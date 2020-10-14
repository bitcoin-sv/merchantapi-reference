// Copyright (c) 2020 Bitcoin Association

using System;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using MerchantAPI.APIGateway.Rest.Swagger;
using MerchantAPI.APIGateway.Rest.ViewModels;
using MerchantAPI.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MerchantAPI.APIGateway.Rest.Services;
using MerchantAPI.APIGateway.Domain.Models;

namespace MerchantAPI.APIGateway.Rest.Controllers
{
  [Route("api/v1/[controller]")]
  [ApiController]
  [Authorize]
  [ApiExplorerSettings(GroupName = SwaggerGroup.Admin)]
  public class StatusController : ControllerBase
  {
    INodes nodes;
    ZMQSubscriptionService subscriptionService;

    public StatusController(
      INodes nodes,
      ZMQSubscriptionService subscriptionService
      )
    {
      this.nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
      this.subscriptionService = subscriptionService ?? throw new ArgumentNullException(nameof(subscriptionService));
    }

    /// <summary>
    /// Get zmq subscriptions status
    /// </summary>
    /// <returns>Zmq subscription status for all nodes.</returns>
    [HttpGet]
    [Route("zmq")]
    public ActionResult<IEnumerable<ZmqStatusViewModelGet>> ZmqStatus()
    {
      var result = nodes.GetNodes();
      return Ok(result.Select(n => new ZmqStatusViewModelGet(n, subscriptionService.GetStatusForNode(n))));
    }
  }
}