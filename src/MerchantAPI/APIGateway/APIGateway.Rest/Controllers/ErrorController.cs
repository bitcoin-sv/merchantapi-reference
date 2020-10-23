// Copyright (c) 2020 Bitcoin Association

using System;
using System.Net;
using MerchantAPI.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
namespace MerchantAPI.APIGateway.Rest.Controllers
{
  [Route("api/v1/[controller]")]
  [ApiController]
  [AllowAnonymous]
  [ApiExplorerSettings(IgnoreApi = true)]
  public class ErrorController : ControllerBase
  {
    private ObjectResult Problem(bool dumpStack)
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