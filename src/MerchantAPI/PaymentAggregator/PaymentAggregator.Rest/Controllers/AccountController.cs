// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using MerchantAPI.PaymentAggregator.Rest.Swagger;
using MerchantAPI.PaymentAggregator.Domain.Repositories;
using MerchantAPI.PaymentAggregator.Rest.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace MerchantAPI.PaymentAggregator.Rest.Controllers
{
  [Route("api/v1/[controller]")]
  [ApiController]
  [Authorize]
  [ApiExplorerSettings(GroupName = SwaggerGroup.Admin)]
  public class AccountController : ControllerBase
  {
    readonly ILogger<AccountController> logger;
    readonly IAccountRepository accountRepository;

    public AccountController(ILogger<AccountController> logger, IAccountRepository accountRepository)
    {
      this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
      this.accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));

    }

    [HttpPost]
    public async Task<ActionResult> Post(AccountViewModelCreate data)
    {
      var domainModel = data.ToDomainModel();
      var errors = domainModel.Validate(new ValidationContext(domainModel));
      if (errors.Count() > 0)
      {
        var pd = ProblemDetailsFactory.CreateProblemDetails(HttpContext, (int)HttpStatusCode.BadRequest);
        pd.Title = string.Join(",", errors.Select(x => x.ErrorMessage));
        return BadRequest(pd);
      }

      var created = await accountRepository.AddAccountAsync(domainModel);
      if (created == null)
      {
        return Conflict();
      }

      return CreatedAtAction(nameof(Get),
                  new { id = created.AccountId },
                  new AccountViewModelGet(created));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> Put(int id, AccountViewModelCreate data)
    {
      var domainModel = data.ToDomainModel();
      var errors = domainModel.Validate(new ValidationContext(domainModel));
      if (errors.Count() > 0)
      {
        var pd = ProblemDetailsFactory.CreateProblemDetails(HttpContext, (int)HttpStatusCode.BadRequest);
        pd.Title = string.Join(",", errors.Select(x => x.ErrorMessage));
        return BadRequest(pd);
      }

      domainModel.AccountId = id;
      var dbAccount = await accountRepository.GetAccountAsync(id);
      if (dbAccount == null)
      {
        return NotFound();
      }
      await accountRepository.UpdateAccountAsync(domainModel);

      return NoContent();
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AccountViewModelGet>>> Get()
    {
      var dbAccounts = await accountRepository.GetAccountsAsync();

      return Ok(dbAccounts.Select(x => new AccountViewModelGet(x)));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<AccountViewModelGet>> Get(int id)
    {
      var dbAccount = await accountRepository.GetAccountAsync(id);
      if (dbAccount == null)
      {
        return NotFound();
      }

      return Ok(new AccountViewModelGet(dbAccount));
    }
  }
}
