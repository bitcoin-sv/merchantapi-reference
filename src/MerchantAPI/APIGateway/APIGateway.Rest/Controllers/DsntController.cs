// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.APIGateway.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;
using System;
using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MerchantAPI.Common.Json;
using NBitcoin.Crypto;
using MerchantAPI.APIGateway.Domain.Models;
using System.IO;
using MerchantAPI.APIGateway.Domain.DSAccessChecks;
using MerchantAPI.APIGateway.Domain.NotificationsHandler;
using MerchantAPI.APIGateway.Domain;
using MerchantAPI.Common.Clock;
using Microsoft.Extensions.Options;
using System.Net.Mime;
using Microsoft.Extensions.Logging;
using MerchantAPI.APIGateway.Rest.Swagger;

namespace MerchantAPI.APIGateway.Rest.Controllers
{
  [Route("[controller]/1")]
  [ServiceFilter(typeof(CheckHostActionFilter))]
  [ApiExplorerSettings(GroupName = SwaggerGroup.API)]
  public class DsntController : Controller
  {
    public const string DSHeader = "x-bsv-dsnt";

    readonly ITxRepository txRepository;
    readonly IRpcMultiClient rpcMultiClient;
    readonly ITransactionRequestsCheck transactionRequestsCheck;
    readonly IClock clock;
    readonly INotificationsHandler notificationsHandler;
    readonly IHostBanList banList;
    readonly ILogger<DsntController> logger;
    readonly AppSettings appSettings;

    public DsntController(ITxRepository txRepository, IRpcMultiClient rpcMultiClient, ITransactionRequestsCheck transactionRequestsCheck, IHostBanList banList,
                          INotificationsHandler notificationsHandler, IClock clock, IOptions<AppSettings> options, ILogger<DsntController> logger)
    {
      this.txRepository = txRepository ?? throw new ArgumentNullException(nameof(txRepository));
      this.rpcMultiClient = rpcMultiClient ?? throw new ArgumentNullException(nameof(rpcMultiClient));
      this.notificationsHandler = notificationsHandler ?? throw new ArgumentNullException(nameof(notificationsHandler));
      this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
      this.transactionRequestsCheck = transactionRequestsCheck ?? throw new ArgumentNullException(nameof(transactionRequestsCheck));
      this.banList = banList ?? throw new ArgumentNullException(nameof(banList));
      this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
      appSettings = options.Value;
    }

    private ObjectResult AddBanScoreAndReturnResult(string error, string txId, int banScore = HostBanList.BanScoreLimit)
    {
      banList.IncreaseBanScore(Request.Host.Host, banScore);
      if (banList.IsHostBanned(Request.Host.Host))
      {
        error += " (banned)";
      }
      logger.LogError($"{error}. Host: {Request.Host.Host}. TxId: {txId}");
      return BadRequest(error);
    }

    [HttpGet]
    [Route("query/{txid}")]
    public async Task<ActionResult> QueryAsync(string txId)
    {
      if (!uint256.TryParse(txId, out uint256 uTxId))
      {
        return AddBanScoreAndReturnResult("Invalid 'txid' format.", "");
      }

      var tx = (await txRepository.GetTxsForDSCheckAsync(new byte[][] { uTxId.ToBytes() }, true)).SingleOrDefault();

      if (tx == null || !tx.DSCheck)
      {
        logger.LogDebug($"Setting not interested information for {txId}");
        // Set response header that we are not interested in DS check
        this.Response.Headers.Add(DSHeader, "0");

        if (tx == null)
        {
          transactionRequestsCheck.LogUnknownTransactionRequest(Request.Host.Host);
        }
        else
        {
          transactionRequestsCheck.LogKnownTransactionId(Request.Host.Host, uTxId);
        }

        if (banList.IsHostBanned(Request.Host.Host))
        {
          return BadRequest("Query for unknown transaction id requested too many times. (banned)");
        }
        return Ok();
      }

      logger.LogDebug($"Setting interested information for {txId}");

      // Set response header that we are interested in DS check
      this.Response.Headers.Add(DSHeader, "1");
      transactionRequestsCheck.LogKnownTransactionId(Request.Host.Host, uTxId);
      if (banList.IsHostBanned(Request.Host.Host))
      {
        return BadRequest("Query for known transaction id requested too many times. (banned)");
      }

      transactionRequestsCheck.LogQueriedTransactionId(Request.Host.Host, uTxId);

      return Ok();
    }

    [ServiceFilter(typeof(CheckHostActionFilter))]
    [HttpPost]
    [Route("submit")]
    [Consumes(MediaTypeNames.Application.Octet)]
    public async Task<ActionResult> SubmitDSAsync([FromQuery]string txId, [FromQuery] int? n, [FromQuery] string cTxId, [FromQuery] int? cn)
    {
      if (string.IsNullOrEmpty(txId))
      {
        return AddBanScoreAndReturnResult("'txid' must not be null or empty.", "");
      }

      if (string.IsNullOrEmpty(cTxId))
      {
        return AddBanScoreAndReturnResult("'ctxid' must not be null or empty.", txId);
      }

      if (!uint256.TryParse(txId, out uint256 uTxId))
      {
        return AddBanScoreAndReturnResult("Invalid 'txid' format.", txId);
      }

      if (!uint256.TryParse(cTxId, out uint256 ucTxId))
      {
        return AddBanScoreAndReturnResult("Invalid 'ctxid' format.", txId);
      }

      if (txId == cTxId)
      {
        return AddBanScoreAndReturnResult("'ctxid' parameter must not be the same as 'txid'.", txId, HostBanList.WarningScore);
      }

      if (n == null || n < 0)
      {
        return AddBanScoreAndReturnResult("'n' must be equal or greater than 0.", txId, HostBanList.WarningScore);
      }

      if (cn == null || cn < 0)
      {
        return AddBanScoreAndReturnResult("'cn' must be equal or greater than 0.", txId, HostBanList.WarningScore);
      }

      if (!transactionRequestsCheck.WasTransactionIdQueried(Request.Host.Host, uTxId))
      {
        return AddBanScoreAndReturnResult("Submitted transactionId was not queried before making a call to submit, or it was already submitted.", txId);
      }

      var tx = (await txRepository.GetTxsForDSCheckAsync(new byte[][] { uTxId.ToBytes() }, true)).SingleOrDefault();

      if (tx == null)
      {
        return BadRequest($"There is no transaction waiting for double-spend notification with given transaction id '{txId}'.");
      }

      transactionRequestsCheck.LogKnownTransactionId(Request.Host.Host, uTxId);

      byte[] dsTxBytes;
      using (var ms = new MemoryStream())
      {
        await Request.Body.CopyToAsync(ms);
        dsTxBytes = ms.ToArray();
      }

      if (dsTxBytes.Length == 0)
      {
        return AddBanScoreAndReturnResult("Proof must not be empty.", txId);
      }

      if (Hashes.DoubleSHA256(dsTxBytes) != ucTxId)
      {
        return AddBanScoreAndReturnResult("Double-spend transaction does not match the 'ctxid' parameter.", txId);
      }

      Transaction dsTx;
      try
      {
        dsTx = HelperTools.ParseBytesToTransaction(dsTxBytes);
      }
      catch (Exception)
      {
        return AddBanScoreAndReturnResult("'dsProof' is invalid.", txId);
      }

      var dsTxIn = dsTx.Inputs[cn.Value];

      if (!(new uint256(tx.TxIn[n.Value].PrevTxId) == dsTxIn.PrevOut.Hash &&
            tx.TxIn[n.Value].PrevN == dsTxIn.PrevOut.N))
      {
        return AddBanScoreAndReturnResult("Transaction marked as double-spend does not spend same inputs as original transaction.", txId);
      }

      logger.LogInformation($"Double spend checks completed successfully for '{txId}' and '{cTxId}'. Verifying script.");

      var scripts = new List<(string Tx, int N)>() 
      { 
        (dsTx.ToHex(), cn.Value)
      }.AsEnumerable();
      
      // We take single result, because we will be sending only 1 tx at a time
      var verified = (await rpcMultiClient.VerifyScriptAsync(true, appSettings.DSScriptValidationTimeoutSec, scripts)).Single();

      if (verified.Result != "ok")
      {
        return AddBanScoreAndReturnResult($"Invalid proof script. Reason: '{verified.Description}'.", cTxId);
      }

      logger.LogInformation($"Successfully verified script for transaction '{cTxId}'. Inserting notification data into database");
      transactionRequestsCheck.RemoveQueriedTransactionId(Request.Host.Host, uTxId);

      var inserted = await txRepository.InsertMempoolDoubleSpendAsync(
                    tx.TxInternalId,
                    dsTx.GetHash().ToBytes(),
                    dsTxBytes);

      if (inserted > 0)
      {
        var notificationData = new NotificationData
        {
          TxExternalId = uTxId.ToBytes(),
          DoubleSpendTxId = ucTxId.ToBytes(),
          CallbackEncryption = tx.CallbackEncryption,
          CallbackToken = tx.CallbackToken,
          CallbackUrl = tx.CallbackUrl,
          TxInternalId = tx.TxInternalId
        };

        await notificationsHandler.EnqueueNotificationAsync(new Domain.Models.Events.NewNotificationEvent
        {
          CreationDate = clock.UtcNow(),
          NotificationData = notificationData,
          NotificationType = CallbackReason.DoubleSpendAttempt,
          TransactionId = uTxId.ToBytes()
        });
        logger.LogInformation($"Inserted notification push data into database for '{txId}'."); 
      }
      return Ok();
    }
  }
}
