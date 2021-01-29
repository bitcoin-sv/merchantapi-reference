// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.APIGateway.Rest.ViewModels;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using MerchantAPI.APIGateway.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using MerchantAPI.APIGateway.Domain.Actions;
using MerchantAPI.APIGateway.Rest.Swagger;

namespace MerchantAPI.APIGateway.Rest.Controllers
{
  [Route("api/v1/[controller]")]
  [ApiController]
  [Authorize]
  [ApiExplorerSettings(GroupName = SwaggerGroup.Admin)]
  public class NodeController : ControllerBase
  {
    INodes nodes;
    IBlockParser blockParser;

    public NodeController(
      INodes nodes,
      IBlockParser blockParser
      )
    {
      this.nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
      this.blockParser = blockParser ?? throw new ArgumentNullException(nameof(blockParser));

    }

    /// <summary>
    /// Register a new node with merchant api.
    /// </summary>
    /// <param name="data"></param>
    /// <returns>New node details.</returns>
    [HttpPost]
    public async Task<ActionResult<NodeViewModelGet>> Post(NodeViewModelCreate data)
    {
      var created = await nodes.CreateNodeAsync(data.ToDomainObject());
      if (created == null)
      {
        var problemDetail = ProblemDetailsFactory.CreateProblemDetails(HttpContext, (int)HttpStatusCode.BadRequest);
        problemDetail.Status = (int)HttpStatusCode.Conflict;
        problemDetail.Title = $"Node '{data.Id}' already exists";
        return Conflict(problemDetail);
      }
      // if this node is first being added on empty DB it will insert bestblockhash
      await blockParser.InitializeDB();

      return CreatedAtAction(nameof(Get),
        new { id = created.ToExternalId() },
        new NodeViewModelGet(created));
    }

    /// <summary>
    /// Update selected node information.
    /// </summary>
    /// <param name="id">Id of the selected node.</param>
    /// <param name="data"></param>
    /// <returns></returns>
    [HttpPut("{id}")]
    public async Task<ActionResult> Put(string id, NodeViewModelCreate data)
    {
      if (!string.IsNullOrEmpty(data.Id) && data.Id.ToLower() != id.ToLower())
      {
        var problemDetail = ProblemDetailsFactory.CreateProblemDetails(HttpContext, (int)HttpStatusCode.BadRequest);
        problemDetail.Title = "The public id does not match the one from message body";
        return BadRequest(problemDetail);
      }

      if (!await nodes.UpdateNodeAsync(data.ToDomainObject()))
      {
        return NotFound();
      }

      return NoContent();
    }

    /// <summary>
    /// Delete selected node.
    /// </summary>
    /// <param name="id">Id of the selected node.</param>
    /// <returns></returns>
    [HttpDelete("{id}")]
    public IActionResult DeleteNode(string id)
    {
      nodes.DeleteNode(id);
      return NoContent();
    }

    /// <summary>
    /// Get selected node details.
    /// </summary>
    /// <param name="id">Id of the selected node.</param>
    /// <returns>Node details.</returns>
    [HttpGet("{id}")]
    public ActionResult<NodeViewModelGet> Get(string id)
    {
      var result = nodes.GetNode(id);
      if (result == null)
      {
        return NotFound();
      }

      return Ok(new NodeViewModelGet(result));
    }

    /// <summary>
    /// Get list of all nodes.
    /// </summary>
    /// <returns>List of nodes.</returns>
    [HttpGet]
    public ActionResult<IEnumerable<NodeViewModelGet>> Get()
    {
      var result = nodes.GetNodes();
      return Ok(result.Select(x => new NodeViewModelGet(x)));
    }
  }
}
