// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mime;
using System.Threading.Tasks;
using MerchantAPI.APIGateway.Rest.ViewModels;
using MerchantAPI.APIGateway.Domain.Actions;
using MerchantAPI.APIGateway.Domain.Models;
using MerchantAPI.APIGateway.Domain.Repositories;
using MerchantAPI.APIGateway.Domain.ViewModels;
using MerchantAPI.Common.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MerchantAPI.APIGateway.Domain;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using NBitcoin;
using MerchantAPI.Common.Clock;
using MerchantAPI.APIGateway.Rest.Swagger;
using MerchantAPI.Common.Authentication;
using MerchantAPI.Common.Exceptions;
using System.Collections.Generic;

namespace MerchantAPI.APIGateway.Rest.Controllers
{
  [Route("mapi")]
  [ApiController]
  [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
  [AllowAnonymous]
  [ApiExplorerSettings(GroupName = SwaggerGroup.API)]
  [ServiceFilter(typeof(HttpsRequiredAttribute))]
  public class MapiController : ControllerBase
  {

    readonly double quoteExpiryMinutes;
    readonly string[] callbackIPAddressesArray;

    readonly IFeeQuoteRepository feeQuoteRepository;
    readonly IMapi mapi;
    readonly ILogger<MapiController> logger;
    readonly IBlockChainInfo blockChainInfo;
    readonly IMinerId minerId;
    readonly IClock clock;
    readonly string[] allowedTxOutFields;

    public MapiController(IFeeQuoteRepository feeQuoteRepository, IMapi mapi, ILogger<MapiController> logger, IBlockChainInfo blockChainInfo, IMinerId minerId, IClock clock, IOptions<AppSettings> options)
    {
      this.feeQuoteRepository = feeQuoteRepository ?? throw new ArgumentNullException(nameof(feeQuoteRepository));
      this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
      this.mapi = mapi ?? throw new ArgumentNullException(nameof(mapi));
      this.blockChainInfo = blockChainInfo ?? throw new ArgumentNullException(nameof(blockChainInfo));
      this.minerId = minerId ?? throw new ArgumentNullException(nameof(minerId));
      this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
      quoteExpiryMinutes = options.Value.QuoteExpiryMinutes.Value;
      callbackIPAddressesArray = options.Value.CallbackIPAddressesArray;
      allowedTxOutFields = options.Value.AllowedTxOutFieldsArray;
    }


    /// <summary>
    /// Signs response if required. If response already contains minerId (for example as part of policy quote), pass it in as currentMinerId
    /// To make sure that correct key is used even key rotation just occured
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="response"></param>
    /// <param name="responseMinerId"></param>
    /// <returns></returns>
    async Task<ActionResult> SignIfRequiredAsync<T>(T response, string responseMinerId)
    {
      string payload = HelperTools.JSONSerialize(response, false);

      if (string.IsNullOrEmpty(responseMinerId))
      {
        // Do not sing if we do not have miner id
        return Ok(new JSONEnvelopeViewModel(payload));
      }


      async Task<(string signature, string publicKey)> SignWithMinerId(string sigHashHex)
      {
        var signature = await minerId.SignWithMinerIdAsync(responseMinerId, sigHashHex);

        return (signature, responseMinerId);
      }

      var jsonEnvelope =  await JsonEnvelopeSignature.CreateJSonSignatureAsync(payload, SignWithMinerId);

      var ret = new SignedPayloadViewModel(jsonEnvelope);

      return Ok(ret);
    }

    private async Task<ActionResult> FillFeeQuoteViewModelWithInfo<T>(T feeQuoteViewModelGet) where T : FeeQuoteViewModelGet
    {
      feeQuoteViewModelGet.ExpiryTime = feeQuoteViewModelGet.Timestamp.Add(TimeSpan.FromMinutes(quoteExpiryMinutes));

      var info = await blockChainInfo.GetInfoAsync();
      feeQuoteViewModelGet.MinerId = await minerId.GetCurrentMinerIdAsync();
      feeQuoteViewModelGet.CurrentHighestBlockHash = info.BestBlockHash;
      feeQuoteViewModelGet.CurrentHighestBlockHeight = info.BestBlockHeight;

      logger.LogInformation($"Returning {nameof(feeQuoteViewModelGet)} with ExpiryTime: {feeQuoteViewModelGet.ExpiryTime}.");

      return await SignIfRequiredAsync(feeQuoteViewModelGet, feeQuoteViewModelGet.MinerId);
    }

    // GET /mapi/feeQuote
    /// <summary>
    /// Get a fee quote.
    /// </summary>
    /// <remarks>This endpoint returns a JSONEnvelope with a payload that contains the fees charged by a specific BSV miner.</remarks>
    [HttpGet]
    [Route("feeQuote")]
    public async Task<ActionResult<FeeQuoteViewModelGet>> GetFeeQuote()
    {
      if (!IdentityProviderStore.GetUserAndIssuer(User, Request.Headers, out var identity))
      {
        return Unauthorized("Incorrectly formatted token");
      }

      logger.LogInformation($"Get FeeQuote for user { ((identity == null) ? "/" : identity.ToString() )} ...");
      FeeQuote feeQuote = feeQuoteRepository.GetCurrentFeeQuoteByIdentity(identity);


      if (feeQuote == null)
      {
        logger.LogInformation($"There are no active feeQuotes.");

        return NotFound();
      }

      var feeQuoteViewModelGet = new FeeQuoteViewModelGet(feeQuote, callbackIPAddressesArray)
      {
        Timestamp = clock.UtcNow(),        
      };

      return await FillFeeQuoteViewModelWithInfo(feeQuoteViewModelGet);
    }

    // GET /mapi/policyQuote
    /// <summary>
    /// Get a policy quote.
    /// </summary>
    /// <remarks>This endpoint returns a JSONEnvelope with a PolicyQuote payload that contains the fees charged by a specific BSV miner and set policies.</remarks>
    [HttpGet]
    [Route("policyQuote")]
    public async Task<ActionResult<PolicyQuoteViewModelGet>> GetPolicyQuote()
    {
      if (!IdentityProviderStore.GetUserAndIssuer(User, Request.Headers, out var identity))
      {
        return Unauthorized("Incorrectly formatted token");
      }

      logger.LogInformation($"Get PolicyQuote for user { ((identity == null) ? "/" : identity.ToString())} ...");
      FeeQuote feeQuote = feeQuoteRepository.GetCurrentFeeQuoteByIdentity(identity);


      if (feeQuote == null)
      {
        logger.LogInformation($"There are no active policyQuotes.");

        return NotFound();
      }

      var policyQuoteViewModelGet = new PolicyQuoteViewModelGet(feeQuote, callbackIPAddressesArray)
      {
        Timestamp = clock.UtcNow(),
      };

      return await FillFeeQuoteViewModelWithInfo(policyQuoteViewModelGet);
    }


    // GET /mapi/tx 
    /// <summary>
    /// Query transaction status.
    /// </summary>
    /// <param name="id">The transaction ID (32 byte hash) hex string.</param>
    /// <param name="merkleProof">Require merkle proof (default=false).</param>
    /// <param name="merkleFormat">Merkle format (default="TSC").</param>
    /// <remarks>This endpoint is used to check the current status of a previously submitted transaction.</remarks>
    [HttpGet]
    [Route("tx/{id}")]
    public async Task<ActionResult<QueryTransactionStatusResponseViewModel>> QueryTransactionStatus(
      string id,
      [FromQuery]
      bool merkleProof = false,
      [FromQuery]
      string merkleFormat = MerkleFormat.TSC)
    {
      if (!IdentityProviderStore.GetUserAndIssuer(User, Request.Headers, out _))
      {
        return Unauthorized("Incorrectly formatted token");
      }
      if (!uint256.TryParse(id, out _))
      {
        var problemDetail = ProblemDetailsFactory.CreateProblemDetails(HttpContext, (int) HttpStatusCode.BadRequest);
        problemDetail.Title = "Invalid format of TransactionId";
        return BadRequest(problemDetail);
      }

      var result =
        new QueryTransactionStatusResponseViewModel(
          await mapi.QueryTransactionAsync(id, merkleProof, merkleFormat));

      return await SignIfRequiredAsync(result, result.MinerId);
    }


    // POST /mapi/tx 
    /// <summary>
    /// Submit a transaction.
    /// </summary>
    /// <param name="data"></param>
    /// <remarks>This endpoint is used to send a raw transaction to a miner for inclusion in the next block that the miner creates.</remarks>
    [HttpPost]
    [Route("tx")]
    [Consumes(MediaTypeNames.Application.Json)]
    public async Task<ActionResult<SubmitTransactionResponseViewModel>> SubmitTxAsync(SubmitTransactionViewModel data)
    {
      if (!IdentityProviderStore.GetUserAndIssuer(User, Request.Headers, out var identity))
      {
        return Unauthorized("Incorrectly formatted token");
      }

      var domainModel = data.ToDomainModel(null, null, null, false, null, false);

      var result =
        new SubmitTransactionResponseViewModel(
          await mapi.SubmitTransactionAsync(domainModel, identity));
      
      return await SignIfRequiredAsync(result, result.MinerId);
    }

    /// <summary>
    /// Submit a transaction in raw format.
    /// </summary>
    /// <param name="callbackUrl">Double spend and merkle proof notification callback endpoint.</param>
    /// <param name="callbackToken">Access token for notification callback endpoint.</param>
    /// <param name="merkleProof">Require merkle proof.</param>
    /// <param name="merkleFormat">Merkle format.</param>
    /// <param name="dsCheck">Check for double spends.</param>
    /// <remarks>This endpoint is used to send a raw transaction to a miner for inclusion in the next block that the miner creates.</remarks>
    [HttpPost]
    [Route("tx")]
    [Consumes(MediaTypeNames.Application.Octet)]
    public async Task<ActionResult<SubmitTransactionResponseViewModel>> SubmitTxRawAsync(
      [FromQuery]
      string callbackUrl,
      [FromQuery]
      string callbackToken,
      [FromQuery]
      string callbackEncryption,
      [FromQuery]
      bool merkleProof,
      [FromQuery]
      string merkleFormat,
      [FromQuery]
      bool dsCheck)
    {
      if (!IdentityProviderStore.GetUserAndIssuer(User, Request.Headers, out var identity))
      {
        return Unauthorized("Incorrectly formatted token");
      }

      byte[] data;
      using (var ms = new MemoryStream())
      {
        await Request.Body.CopyToAsync(ms);
        data = ms.ToArray();
      }

      var request = new SubmitTransaction
      {
        RawTx = data,
        CallbackUrl = callbackUrl,
        CallbackToken = callbackToken,
        CallbackEncryption =  callbackEncryption,
        MerkleProof = merkleProof,
        MerkleFormat = merkleFormat,
        DsCheck = dsCheck
      };
      
      var result = new SubmitTransactionResponseViewModel(await mapi.SubmitTransactionAsync(request, identity));
      return await SignIfRequiredAsync(result, result.MinerId);
    }

    // POST /mapi/txs
    /// <summary>
    /// Submit multiple transactions.
    /// </summary>
    /// <param name="data"></param>
    /// <param name="defaultCallbackUrl">Default double spend and merkle proof notification callback endpoint.</param>
    /// <param name="defaultCallbackToken">Default access token for notification callback endpoint.</param>
    /// <param name="defaultMerkleProof">Default merkle proof requirement.</param>
    /// <param name="defaultDsCheck">Default double spend notification request.</param>
    /// <remarks>This endpoint is used to send multiple raw transactions to a miner for inclusion in the next block that the miner creates.</remarks>
    [HttpPost]
    [Route("txs")]
    [Consumes(MediaTypeNames.Application.Json)]
    public async Task<ActionResult<SubmitTransactionResponseViewModel>> SubmitTxsAsync(
      SubmitTransactionViewModel[] data,
      [FromQuery]
      string defaultCallbackUrl,
      [FromQuery]
      string defaultCallbackToken,
      [FromQuery]
      string defaultCallbackEncryption,
      [FromQuery]
      bool defaultMerkleProof,
      [FromQuery]
      string defaultMerkleFormat,
      [FromQuery]
      bool defaultDsCheck)
    {
      if (!IdentityProviderStore.GetUserAndIssuer(User, Request.Headers, out var identity))
      {
        return Unauthorized("Incorrectly formatted token");
      }
      
      var domainModel = data.Select(x =>
        x.ToDomainModel(defaultCallbackUrl, defaultCallbackToken, defaultCallbackEncryption, defaultMerkleProof, defaultMerkleFormat, defaultDsCheck)).ToArray();

      SubmitTransactionsResponseViewModel result;
      try
      {
        result =
          new SubmitTransactionsResponseViewModel(
            await mapi.SubmitTransactionsAsync(domainModel,
              identity));
      }
      catch(BadRequestException ex)
      {
        logger.LogError($"Error while submiting transactions. {ex.Message}.");
        var problemDetail = ProblemDetailsFactory.CreateProblemDetails(HttpContext, (int)HttpStatusCode.BadRequest);
        problemDetail.Title = ex.Message;
        return BadRequest(problemDetail);
      }

      return await SignIfRequiredAsync(result, result.MinerId);

    }

    // POST /mapi/txs
    /// <summary>
    /// 
    /// </summary>
    /// <param name="callbackUrl"></param>
    /// <param name="merkleProof"></param>
    /// <param name="dsCheck"></param>
    /// <remarks>Multiple Transactions can be provided in body. Other parameters (such as callbackUrl) applies to all transactions.</remarks>
    [HttpPost]
    [Route("txs")]
    [Consumes(MediaTypeNames.Application.Octet)]
    public async Task<ActionResult<SubmitTransactionResponseViewModel>> SubmitTxsRawAsync(
      [FromQuery] string callbackUrl,
      [FromQuery] string callbackEncryption,
      [FromQuery] string callbackToken,
      [FromQuery] bool merkleProof,
      [FromQuery] string merkleFormat,
      [FromQuery] bool dsCheck
      )
    {
      if (!IdentityProviderStore.GetUserAndIssuer(User, Request.Headers, out var identity))
      {
        return Unauthorized("Incorrectly formatted token");
      }
      // callbackUrl is validated as part of domainObject - no special validation here
      byte[] data;
      using (var ms = new MemoryStream())
      {
        await Request.Body.CopyToAsync(ms);
        data = ms.ToArray();
      }

      byte[][] transactionAsBytes;
      try
      {
        transactionAsBytes = HelperTools.ParseTransactionsIntoBytes(data);
      }
      catch (Exception)
      {
        var problemDetail = ProblemDetailsFactory.CreateProblemDetails(HttpContext, (int)HttpStatusCode.BadRequest);
        problemDetail.Title = "Unable to parse incoming stream of transactions";
        return BadRequest(problemDetail);
      }
      
      var request = transactionAsBytes.Select(

        t =>
          new SubmitTransaction
          {
            RawTx = t,
            CallbackUrl = callbackUrl,
            CallbackEncryption = callbackEncryption,
            CallbackToken = callbackToken,
            MerkleProof = merkleProof,
            MerkleFormat = merkleFormat,
            DsCheck = dsCheck
          }).ToArray();

      SubmitTransactionsResponseViewModel result;
      try
      {
        result =
          new SubmitTransactionsResponseViewModel(
            await mapi.SubmitTransactionsAsync(request, identity));
      }
      catch (BadRequestException ex)
      {
        logger.LogError($"Error while submitting transactions. {ex.Message}.");
        var problemDetail = ProblemDetailsFactory.CreateProblemDetails(HttpContext, (int)HttpStatusCode.BadRequest);
        problemDetail.Title = ex.Message;
        return BadRequest(problemDetail);
      }
      return await SignIfRequiredAsync(result, result.MinerId);
    }

    // POST /mapi/txouts
    /// <summary>
    /// Get details about an unspent transaction outputs.
    /// </summary>
    /// <param name="txOuts">POST list of txouts to query.</param>
    /// <param name="returnField">Fields to be returned. If omitted, all fields will be returned.</param>
    /// <param name="includeMempool">Whether to include the mempool.</param>
    /// <remarks>This endpoint is used to query unspent transaction outputs.</remarks>
    [HttpPost]
    [Route("txouts")]
    public async Task<ActionResult<TxOutsResponseViewModel>> GetTxOuts(
      TxOutsRequestViewModel[] txOuts,
      [FromQuery]
      string[] returnField,
      [FromQuery]
      bool includeMempool = true)
    {
      string[] selectedFields = allowedTxOutFields;
      if (!IdentityProviderStore.GetUserAndIssuer(User, Request.Headers, out _))
      {
        return Unauthorized("Incorrectly formatted token");
      }

      if (returnField.Length > 0)
      {
        if (returnField.Any(f => !allowedTxOutFields.Any(a => a.ToLower() == f.ToLower())))
        {
          var problemDetail = ProblemDetailsFactory.CreateProblemDetails(HttpContext, (int)HttpStatusCode.BadRequest);
          problemDetail.Title = $"Invalid return field selected. Allowed fields: {string.Join(",", allowedTxOutFields)}.";
          return BadRequest(problemDetail);
        }
        // bitcoind requires correct casing so select from predefined values
        selectedFields = allowedTxOutFields.Where(a => returnField.Any(f => f.ToLower() == a.ToLower())).ToArray();
      }

      foreach (var txOut in txOuts)
      {
        if (!uint256.TryParse(txOut.TxId, out _))
        {
          var problemDetail = ProblemDetailsFactory.CreateProblemDetails(HttpContext, (int)HttpStatusCode.BadRequest);
          problemDetail.Title = @"Invalid format of TransactionId {txOut.TxId}.";
          return BadRequest(problemDetail);
        }
      }

      var result =
        new TxOutsResponseViewModel(await mapi.GetTxOutsAsync(txOuts.Select(t => (t.TxId, t.N)), selectedFields, includeMempool));

      return await SignIfRequiredAsync(result, result.MinerId);
    }
  }

}