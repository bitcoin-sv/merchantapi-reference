// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using System.Collections.Generic;
using System.Linq;
using MerchantAPI.APIGateway.Rest.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MerchantAPI.APIGateway.Rest.Services;
using MerchantAPI.APIGateway.Domain.Models;
using MerchantAPI.APIGateway.Rest.Swagger;
using MerchantAPI.APIGateway.Domain.Actions;
using System.Threading.Tasks;
using MerchantAPI.APIGateway.Domain;
using Microsoft.Extensions.Options;

namespace MerchantAPI.APIGateway.Rest.Controllers
{
  [Route("api/v1/[controller]")]
  [ApiController]
  [Authorize]
  [ApiExplorerSettings(GroupName = SwaggerGroup.Admin)]
  [ServiceFilter(typeof(HttpsRequiredAttribute))]
  public class StatusController : ControllerBase
  {
    readonly INodes nodes;
    readonly IBlockParser blockParser;
    readonly AppSettings appSettings;
    readonly ZMQSubscriptionService subscriptionService;

    public StatusController(
      INodes nodes,
      IBlockParser blockParser,
      IOptions<AppSettings> options,
      ZMQSubscriptionService subscriptionService
      )
    {
      this.nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
      this.blockParser = blockParser ?? throw new ArgumentNullException(nameof(blockParser));
      appSettings = options.Value;
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

    /// <summary>
    /// Get block parser status
    /// </summary>
    /// <returns>Block parser status and description.</returns>
    [HttpGet]
    [Route("blockParser")]
    public async Task<ActionResult<BlockParserStatusViewModelGet>> BlockParserStatus()
    {
      var status = await blockParser.GetBlockParserStatusAsync();
      return Ok(new BlockParserStatusViewModelGet(status,
        appSettings.DontParseBlocks.Value,
        appSettings.DontInsertTransactions.Value,
        appSettings.DeltaBlockHeightForDoubleSpendCheck.Value,
        appSettings.MaxBlockChainLengthForFork.Value));
    }
  }
}