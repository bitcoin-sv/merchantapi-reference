// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using System.Net;
using MerchantAPI.Common.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace MerchantAPI.APIGateway.Rest.Controllers
{
  [Route("api/v1/[controller]")]
  [ApiController]
  [AllowAnonymous]
  [ApiExplorerSettings(IgnoreApi = true)]
  public class ErrorController : ControllerBase
  {
    readonly ILogger<ErrorController> logger;
    public ErrorController(ILogger<ErrorController> logger)
    {
      this.logger = logger;
    }

    private ObjectResult Problem(bool dumpStack)
    {
      var ex = HttpContext.Features.Get<IExceptionHandlerPathFeature>().Error;
      string title = "Internal system error occurred, please retry later";
      var statusCode = (int)HttpStatusCode.InternalServerError;

      if (ex is HttpResponseException response)
      {
        statusCode = response.Status;

        // DomainException same as InternalServerError
        if (ex is BadRequestException)
        {
          title = "Bad client request";
        }
        else if (ex is ServiceUnavailableException)
        {
          title = "Service unavailable";
        }
        else if (ex is NotFoundException)
        {
          title = "Not found";
        }
      }

      logger.LogError(ex, "Error while performing operation");

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
    public IActionResult ErrorLocalDevelopment([FromServices] IWebHostEnvironment webHostEnvironment)
    {
      if (webHostEnvironment.EnvironmentName != "Development")
      {
        throw new InvalidOperationException("This shouldn't be invoked in non-development environments.");
      }
      return Problem(dumpStack: true);
    }

    // In non development mode we don't return stack trace
    [Route("/error")]
    public IActionResult Error()
    {
      return Problem(dumpStack: false);
    }
  }
}