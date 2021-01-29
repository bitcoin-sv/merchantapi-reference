// Copyright (c) 2020 Bitcoin Association

using System;
using System.Net;
using System.Threading.Tasks;
using MerchantAPI.Common.Exceptions;
using MerchantAPI.PaymentAggregator.Consts;
using MerchantAPI.PaymentAggregator.Domain.Models;
using MerchantAPI.PaymentAggregator.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
namespace MerchantAPI.PaymentAggregator.Rest.Controllers
{
  [Route("api/v1/[controller]")]
  [ApiController]
  [AllowAnonymous]
  [ApiExplorerSettings(IgnoreApi = true)]
  public class ErrorController : ControllerBase
  {
    readonly IServiceRequestRepository serviceRequestRepository;

    public ErrorController(IServiceRequestRepository serviceRequestRepository) : base()
    {
      this.serviceRequestRepository = serviceRequestRepository ?? throw new ArgumentNullException(nameof(serviceRequestRepository));
    }
    private async Task<ObjectResult> ProblemAsync(bool dumpStack)
    {
      var ex = HttpContext.Features.Get<IExceptionHandlerPathFeature>().Error;
      string title = string.Empty;
      var statusCode = (int)HttpStatusCode.InternalServerError;

      if (ex is DomainException)
      {
        title = "Internal system error occurred";
        statusCode = (int)HttpStatusCode.InternalServerError;
      }
      if (ex is BadRequestException)
      {
        title = "Bad client request";
        statusCode = (int)HttpStatusCode.BadRequest;
      }
      if (ex is ServiceUnavailableException)
      {
        title = "Service unavailable";
        statusCode = (int)HttpStatusCode.ServiceUnavailable;
      }
      // Log service request
      if (ex.Data.Contains(Const.EXCEPTION_DETAILS_EXECUTION_TIME) && 
          ex.Data.Contains(Const.EXCEPTION_DETAILS_SUBSCRIPTION_ID))
      {
        await InsertServiceRequestAsync(
          (int)ex.Data[Const.EXCEPTION_DETAILS_SUBSCRIPTION_ID],
          statusCode,
          (long)ex.Data[Const.EXCEPTION_DETAILS_EXECUTION_TIME]
        );
      }
      var pd = ProblemDetailsFactory.CreateProblemDetails(
        HttpContext,
        statusCode: statusCode,
        title: title,
        detail: ex.Message);
      if (dumpStack)
      {
        pd.Extensions.Add("stackTrace", ex.ToString());
      }

      var result = new ObjectResult(pd)
      {
        StatusCode = statusCode
      };
      return result;
    }

    [Route("/error-development")]
    public async Task<IActionResult> ErrorLocalDevelopment([FromServices] IWebHostEnvironment webHostEnvironment)
    {
      if (webHostEnvironment.EnvironmentName != "Development")
      {
        throw new InvalidOperationException("This shouldn't be invoked in non-development environments.");
      }
      return await ProblemAsync(dumpStack: true);
    }

    // In non development mode we don't return stack trace
    [Route("/error")]
    public async Task<IActionResult> Error()
    {
      return await ProblemAsync(dumpStack: false);
    }

    private async Task InsertServiceRequestAsync(int subscriptionId, int responseCode, long executionTimeMs)
    {
      var serviceRequest = new ServiceRequest()
      {
        SubscriptionId = subscriptionId,
        ResponseCode = responseCode,
        ExecutionTimeMs = executionTimeMs
      };
      await serviceRequestRepository.InsertServiceRequestAsync(serviceRequest);
    }
  }
}