// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.PaymentAggregator.Rest.Swagger;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using MerchantAPI.PaymentAggregator.Domain.Repositories;
using MerchantAPI.PaymentAggregator.Rest.ViewModels;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using System.Net;
using NBitcoin;
using MerchantAPI.PaymentAggregator.Domain.Models;
using MerchantAPI.PaymentAggregator.Domain.ViewModels;
using System.Diagnostics;
using System.Net.Mime;
using System.IO;
using MerchantAPI.PaymentAggregator.Consts;
using MerchantAPI.PaymentAggregator.Rest.Actions;

namespace MerchantAPI.PaymentAggregator.Rest.Controllers
{
  [Route("api/v1")]
  [ApiController]
  [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
  [ApiExplorerSettings(GroupName = SwaggerGroup.API)]
  public class AggregatorController : BaseControllerWithAccount
  {
    readonly IAggregator aggregator;
    readonly IServiceRequestRepository serviceRequestRepository;

    public AggregatorController(ILogger<AggregatorController> logger, IAggregator aggregator, IServiceRequestRepository serviceRequestRepository, ISubscriptionRepository subscriptionRepository, IAccountRepository accountRepository)
      : base(logger, subscriptionRepository, accountRepository)
    {
      this.aggregator = aggregator ?? throw new ArgumentNullException(nameof(aggregator));
      this.serviceRequestRepository = serviceRequestRepository ?? throw new ArgumentNullException(nameof(serviceRequestRepository));
    }

    private async Task InsertServiceRequestAsync(int subscriptionId, int responseCode, Stopwatch stopWatch)
    {
      var serviceRequest = new ServiceRequest()
      {
        SubscriptionId = subscriptionId,
        ResponseCode = responseCode,
        ExecutionTimeMs = stopWatch.ElapsedMilliseconds
      };
      await serviceRequestRepository.InsertServiceRequestAsync(serviceRequest);
    }

    // GET /api/v1/allfeequotes
    /// <summary>
    /// Get all fee quotes from miners running mAPI.
    /// </summary>
    /// <remarks>Returns an array of miner fee quotes together with calculated SLA-s.</remarks>
    [HttpGet] 
    [Route("allfeequotes")]
    public async Task<ActionResult<AllFeeQuotesViewModelGet>> GetAllFeeQuotes()
    {
      var (account, subscriptionId, actionResult) = await ValidateAccountAndSubscriptionAsync(User, Request.Headers, Consts.ServiceType.allFeeQuotes);
      if ( actionResult != null)
      {
        return actionResult;
      }
      
      var watch = Stopwatch.StartNew();
      AllFeeQuotesViewModelGet result;
      try
      {
        result = await aggregator.GetAllFeeQuotesAsync();
        watch.Stop();
      }
      catch (Exception ex)
      {
        watch.Stop();
        ex.Data.Add(Const.EXCEPTION_DETAILS_EXECUTION_TIME, watch.ElapsedMilliseconds);
        ex.Data.Add(Const.EXCEPTION_DETAILS_SUBSCRIPTION_ID, subscriptionId.Value);
        throw;
      }

      if (result == null)
      {
        return await GetActionResultAndInsertLogAsync((int)HttpStatusCode.NotFound, () => NotFound(), subscriptionId.Value, watch);
      }
      return await GetActionResultAndInsertLogAsync((int)HttpStatusCode.OK, () => Ok(result), subscriptionId.Value, watch);
    }

    private async Task<ActionResult> GetActionResultAndInsertLogAsync(int statusCode, Func<ActionResult> action, int subscriptionId, Stopwatch watch)
    {
      await InsertServiceRequestAsync(subscriptionId, statusCode, watch);
      return action();
    }

    // GET /api/v1/tx 
    /// <summary>
    /// Get transaction status from all miners running mAPI.
    /// </summary>
    /// <param name="id">The transaction ID (32 byte hash) hex string</param>
    /// <remarks>This endpoint is used to check the current status of a previously submitted transaction. Returns array of current statuses returned by miners.</remarks>
    [HttpGet]
    [Route("tx/{id}")]
    public async Task<ActionResult<QueryTransactionStatusResponseViewModel[]>> QueryTransactionStatuses(string id)
    {
      var (account, subscriptionId, actionResult) = await ValidateAccountAndSubscriptionAsync(User, Request.Headers, Consts.ServiceType.queryTx);
      if (actionResult != null)
      {
        return actionResult;
      }

      if (!uint256.TryParse(id, out _))
      {
        var problemDetail = ProblemDetailsFactory.CreateProblemDetails(HttpContext, (int)HttpStatusCode.BadRequest);
        problemDetail.Title = "Invalid format of TransactionId";
        return BadRequest(problemDetail);
      }

      var watch = Stopwatch.StartNew();
      SignedPayloadViewModel[] result;
      try
      {
        result = await aggregator.QueryTransactionStatusesAsync(id);
        watch.Stop();
      }
      catch (Exception ex)
      {
        watch.Stop();
        ex.Data.Add(Const.EXCEPTION_DETAILS_EXECUTION_TIME, watch.ElapsedMilliseconds);
        ex.Data.Add(Const.EXCEPTION_DETAILS_SUBSCRIPTION_ID, subscriptionId.Value);
        throw;
      }

      return await GetActionResultAndInsertLogAsync((int)HttpStatusCode.OK, () => Ok(result), subscriptionId.Value, watch);
    }

    // POST /api/v1/tx  
    /// <summary>
    /// Submit a transaction.
    /// </summary>
    /// <param name="data"></param>
    /// <remarks>This endpoint is used to send a raw transaction to miners for inclusion in the next block that will be created.</remarks>
    [HttpPost]
    [Route("tx")]
    [Consumes(MediaTypeNames.Application.Json)]
    public async Task<ActionResult<SubmitTransactionResponseViewModel[]>> SubmitTxAsync(SubmitTransactionViewModel data)
    {
      var (account, subscriptionId, actionResult) = await ValidateAccountAndSubscriptionAsync(User, Request.Headers, Consts.ServiceType.submitTx);
      if (actionResult != null)
      {
        return actionResult;
      }
      var watch = Stopwatch.StartNew();
      SignedPayloadViewModel[] result;

      try
      {
        result = await aggregator.SubmitTransactionAsync(data);
        watch.Stop();
      }
      catch (Exception ex)
      {
        watch.Stop();
        ex.Data.Add(Const.EXCEPTION_DETAILS_EXECUTION_TIME, watch.ElapsedMilliseconds);
        ex.Data.Add(Const.EXCEPTION_DETAILS_SUBSCRIPTION_ID, subscriptionId.Value);
        throw;
      }     
      return await GetActionResultAndInsertLogAsync((int)HttpStatusCode.OK, () => Ok(result), subscriptionId.Value, watch);
    }

    // POST /api/v1/tx  
    /// <summary>
    /// Submit a transaction in raw format.
    /// </summary>
    /// <param name="callbackUrl">Double spend and merkle proof notification callback endpoint.</param>
    /// <param name="callbackToken">Access token for notification callback endpoint.</param>
    /// <param name="merkleProof">Require merkle proof</param>
    /// <param name="dsCheck">Check for double spends.</param>
    /// <remarks>This endpoint is used to send a raw transaction to a miners for inclusion in the next block that will be created.</remarks>
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
      var (account, subscriptionId, actionResult) = await ValidateAccountAndSubscriptionAsync(User, Request.Headers, Consts.ServiceType.submitTx);
      if (actionResult != null)
      {
        return actionResult;
      }
      var watch = Stopwatch.StartNew();
      SignedPayloadViewModel[] result;
      try
      {
        byte[] data;
        using (var ms = new MemoryStream())
        {
          await Request.Body.CopyToAsync(ms);
          data = ms.ToArray();
        }
        result = await aggregator.SubmitRawTransactionAsync(data, callbackUrl, callbackToken, callbackEncryption, merkleProof, dsCheck);
        watch.Stop();
      }
      catch (Exception ex)
      {
        watch.Stop();
        ex.Data.Add(Const.EXCEPTION_DETAILS_EXECUTION_TIME, watch.ElapsedMilliseconds);
        ex.Data.Add(Const.EXCEPTION_DETAILS_SUBSCRIPTION_ID, subscriptionId.Value);
        throw;
      }
      return await GetActionResultAndInsertLogAsync((int)HttpStatusCode.OK, () => Ok(result), subscriptionId.Value, watch);
    }

    // POST /api/v1/txs
    /// <summary>
    /// Submit multiple transactions.
    /// </summary>
    /// <param name="data"></param>
    /// <remarks>This endpoint is used to send multiple raw transactions to miners for inclusion in the next block that will be created.</remarks>
    [HttpPost]
    [Route("txs")]
    [Consumes(MediaTypeNames.Application.Json)]
    public async Task<ActionResult<SubmitTransactionResponseViewModel[]>> SubmitTxsAsync(SubmitTransactionViewModel[] data)
    {
      var (account, subscriptionId, actionResult) = await ValidateAccountAndSubscriptionAsync(User, Request.Headers, Consts.ServiceType.submitTx);
      if (actionResult != null)
      {
        return actionResult;
      }
      var watch = Stopwatch.StartNew();
      SignedPayloadViewModel[] result;
      try
      {
        result = await aggregator.SubmitTransactionsAsync(data);
        watch.Stop();
      }
      catch (Exception ex)
      {
        watch.Stop();
        ex.Data.Add(Const.EXCEPTION_DETAILS_EXECUTION_TIME, watch.ElapsedMilliseconds);
        ex.Data.Add(Const.EXCEPTION_DETAILS_SUBSCRIPTION_ID, subscriptionId.Value);
        throw;
      }
      return await GetActionResultAndInsertLogAsync((int)HttpStatusCode.OK, () => Ok(result), subscriptionId.Value, watch);
    }

    // POST /api/v1/txs
    /// <summary>
    /// Submit multiple transactions in raw format.
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
      var (account, subscriptionId, actionResult) = await ValidateAccountAndSubscriptionAsync(User, Request.Headers, Consts.ServiceType.submitTx);
      if (actionResult != null)
      {
        return actionResult;
      }
      var watch = Stopwatch.StartNew();
      SignedPayloadViewModel[] result;
      try
      {
        byte[] data;
        using (var ms = new MemoryStream())
        {
          await Request.Body.CopyToAsync(ms);
          data = ms.ToArray();
        }
        result = await aggregator.SubmitRawTransactionsAsync(data, callbackUrl, callbackToken, callbackEncryption, merkleProof, dsCheck);
        watch.Stop();
      }
      catch (Exception ex)
      {
        watch.Stop();
        ex.Data.Add(Const.EXCEPTION_DETAILS_EXECUTION_TIME, watch.ElapsedMilliseconds);
        ex.Data.Add(Const.EXCEPTION_DETAILS_SUBSCRIPTION_ID, subscriptionId.Value);
        throw;
      }
      return await GetActionResultAndInsertLogAsync((int)HttpStatusCode.OK, () => Ok(result), subscriptionId.Value, watch);
    }

  }
}