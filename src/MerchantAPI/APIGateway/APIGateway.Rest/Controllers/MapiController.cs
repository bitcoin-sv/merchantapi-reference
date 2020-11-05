// Copyright (c) 2020 Bitcoin Association

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
using MerchantAPI.APIGateway.Rest.Authentication;
using MerchantAPI.Common.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MerchantAPI.APIGateway.Domain;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using MerchantAPI.APIGateway.Rest.Swagger;
using NBitcoin;
using Newtonsoft.Json.Linq;
using MerchantAPI.Common.Clock;

namespace MerchantAPI.APIGateway.Rest.Controllers
{
  [Route("mapi")]
  [ApiController]
  [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
  [AllowAnonymous]
  [ApiExplorerSettings(GroupName = SwaggerGroup.API)]
  public class MapiController : ControllerBase
  {

    private readonly double quoteExpiryMinutes;
    
    IFeeQuoteRepository feeQuoteRepository;
    IMapi mapi;
    ILogger<MapiController> logger;
    IBlockChainInfo blockChainInfo;
    IMinerId minerId;
    private readonly IClock clock;


    public MapiController(IOptions<AppSettings> options, IFeeQuoteRepository feeQuoteRepository, IMapi mapi, ILogger<MapiController> logger, IBlockChainInfo blockChainInfo, IMinerId minerId, IClock clock)
    {
      this.feeQuoteRepository = feeQuoteRepository ?? throw new ArgumentNullException(nameof(feeQuoteRepository));
      this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
      this.mapi = mapi ?? throw new ArgumentNullException(nameof(mapi));
      this.blockChainInfo = blockChainInfo ?? throw new ArgumentNullException(nameof(blockChainInfo));
      this.minerId = minerId ?? throw new ArgumentNullException(nameof(minerId));
      this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
      quoteExpiryMinutes = options.Value.QuoteExpiryMinutes;
    }


    /// <summary>
    /// Signs response if required. If response already contains minerId (for example as part of fee quote), pass it in as currentMinerId
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
        return Ok(new JSONEnvelopeViewModelGet(payload));
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

      var feeQuoteViewModelGet = new FeeQuoteViewModelGet(feeQuote)
      {
        Timestamp = clock.UtcNow(),        
      };
      feeQuoteViewModelGet.ExpiryTime = feeQuoteViewModelGet.Timestamp.Add(TimeSpan.FromMinutes(quoteExpiryMinutes));

      var info = blockChainInfo.GetInfo();
      feeQuoteViewModelGet.MinerId = await minerId.GetCurrentMinerIdAsync();
      feeQuoteViewModelGet.CurrentHighestBlockHash = info.BestBlockHash;
      feeQuoteViewModelGet.CurrentHighestBlockHeight = info.BestBlockHeight;

      logger.LogInformation($"Returning feeQuote with ExpiryTime: {feeQuoteViewModelGet.ExpiryTime}.");

      return await SignIfRequiredAsync(feeQuoteViewModelGet, feeQuoteViewModelGet.MinerId);
    }



    // GET /mapi/tx 
    /// <summary>
    /// Query transaction status.
    /// </summary>
    /// <param name="id">The transaction ID (32 byte hash) hex string</param>
    /// <remarks>This endpoint is used to check the current status of a previously submitted transaction.</remarks>
    [HttpGet]
    [Route("tx/{id}")]
    public async Task<ActionResult<QueryTransactionStatusResponseViewModel>> QueryTransactionStatus(string id)
    {
      if (!IdentityProviderStore.GetUserAndIssuer(User, Request.Headers, out var identity))
      {
        return Unauthorized("Incorrectly formatted token");
      }
      if (!uint256.TryParse(id, out _))
      {
        var problemDetail = ProblemDetailsFactory.CreateProblemDetails(HttpContext, (int) HttpStatusCode.BadRequest);
        problemDetail.Title = "invalid format of TransactionId";
        return BadRequest(problemDetail);
      }

      var result =
        new QueryTransactionStatusResponseViewModel(
          await mapi.QueryTransaction(id));

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

      var domainModel = data.ToDomainModel(null, null, null, false, false);

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
    /// <param name="merkleProof">Require merkle proof</param>
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
      bool defaultDsCheck)
    {
      if (!IdentityProviderStore.GetUserAndIssuer(User, Request.Headers, out var identity))
      {
        return Unauthorized("Incorrectly formatted token");
      }
      
      var domainModel = data.Select(x =>
        x.ToDomainModel(defaultCallbackUrl, defaultCallbackToken, defaultCallbackEncryption, defaultMerkleProof, defaultDsCheck)).ToArray();

      var result =
        new SubmitTransactionsResponseViewModel(
          await mapi.SubmitTransactionsAsync(domainModel,
            identity));

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
        // This is not very efficient, since we will reparse bytes into NBitcoin.Transaction later again
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
            DsCheck = dsCheck
          }).ToArray();

      var result =
        new SubmitTransactionsResponseViewModel(
          await mapi.SubmitTransactionsAsync(request, identity));
      return await SignIfRequiredAsync(result, result.MinerId);
    }
  }

}