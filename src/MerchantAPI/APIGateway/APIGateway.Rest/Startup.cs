// Copyright (c) 2020 Bitcoin Association

using System;
using MerchantAPI.APIGateway.Infrastructure.Repositories;
using MerchantAPI.Common.BitcoinRpc;
using MerchantAPI.APIGateway.Domain.Actions;
using MerchantAPI.APIGateway.Domain.Models;
using MerchantAPI.APIGateway.Domain.Repositories;
using MerchantAPI.APIGateway.Rest.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MerchantAPI.APIGateway.Rest.Services;
using MerchantAPI.Common.EventBus;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using MerchantAPI.APIGateway.Domain;
using MerchantAPI.APIGateway.Domain.ExternalServices;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.OpenApi.Models;
using System.Linq;
using System.Net.Http;
using MerchantAPI.APIGateway.Rest.Swagger;
using MerchantAPI.Common.Clock;
using MerchantAPI.Common.Database;
using MerchantAPI.APIGateway.Domain.NotificationsHandler;

namespace MerchantAPI.APIGateway.Rest
{

  public class Startup
  {

    IWebHostEnvironment HostEnvironment { get; set; }

    public Startup(IConfiguration configuration, IWebHostEnvironment hostEnvironment)
    {
      Configuration = configuration;
      this.HostEnvironment = hostEnvironment;
    }

    public IConfiguration Configuration { get; }

    // This method gets called by the runtime. Use this method to add services to the container.
    public virtual void ConfigureServices(IServiceCollection services)
    {
      // time in database is UTC so it is automatically mapped to Kind=UTC
      Dapper.SqlMapper.AddTypeHandler(new Common.DateTimeHandler());

      services.AddOptions<IdentityProviders>()
        .Bind(Configuration.GetSection("IdentityProviders"))
        .ValidateDataAnnotations();

      services.AddOptions<AppSettings>()
        .Bind(Configuration.GetSection("AppSettings"))
        .ValidateDataAnnotations();


      services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<AppSettings>, AppSettingValidator>());
      services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<IdentityProviders>, IdentityProvidersValidator>());

      services.AddAuthentication(options =>
      {
        options.DefaultAuthenticateScheme = ApiKeyAuthenticationOptions.DefaultScheme;
        options.DefaultChallengeScheme = ApiKeyAuthenticationOptions.DefaultScheme;
        options.AddScheme(ApiKeyAuthenticationOptions.DefaultScheme, a => a.HandlerType = typeof(ApiKeyAuthenticationHandler));
      });


      services.AddControllers().AddJsonOptions(options => { options.JsonSerializerOptions.WriteIndented = true; });

      services.AddSingleton<IEventBus, InMemoryEventBus>();

      services.AddTransient<IFeeQuoteRepository, FeeQuoteRepositoryPostgres>(); 

      services.AddTransient<INodes, Nodes>();
      services.AddTransient<INodeRepository, NodeRepositoryPostgres>();
      services.AddTransient<ITxRepository, TxRepositoryPostgres>();
      services.AddTransient<IMapi, Mapi>();
      services.AddTransient<IRpcClientFactory, RpcClientFactory>();
      services.AddTransient<IRpcMultiClient, RpcMultiClient>();
      services.AddSingleton<INotificationServiceHttpClientFactory, NotificationServiceHttpClientFactoryDefault>();
      services.AddHttpClient(NotificationServiceHttpClientFactoryDefault.ClientName) 
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
          UseCookies =
            false, // Disable cookies they are not needed and we do not want to leak them - https://docs.microsoft.com/en-us/aspnet/core/fundamentals/http-requests?view=aspnetcore-3.1#cookies
        });

      services.AddHttpClient("minerIdClient"); // will only be used if WifPrivateKey is not provided
      services.AddSingleton<IBlockChainInfo, BlockChainInfo>(); // singleton, thread safe
      services.AddSingleton<IBlockParser, BlockParser>(); // singleton, thread safe
      services.AddTransient<ICreateDB, CreateDB>();
      services.AddSingleton<INotificationsHandler, NotificationsHandler>();// singleton, thread safe

      services.AddHostedService(p => (BlockChainInfo)p.GetRequiredService<IBlockChainInfo>());
      services.AddHostedService(p => (NotificationsHandler)p.GetRequiredService<INotificationsHandler>());



      services.AddSingleton<IMinerId>(s =>
        {
          var appSettings = s.GetService<IOptions<AppSettings>>().Value;
          var httpClientFactory = s.GetRequiredService<IHttpClientFactory>();

          if (!string.IsNullOrWhiteSpace(appSettings.WifPrivateKey))
          {
            return new MinerIdFromWif(appSettings.WifPrivateKey);
          }
          else if (appSettings.MinerIdServer != null && !string.IsNullOrEmpty(appSettings.MinerIdServer.Url))
          {
            return new MinerIdRestClient(appSettings.MinerIdServer.Url, appSettings.MinerIdServer.Alias, appSettings.MinerIdServer.Authentication, 
              httpClientFactory.CreateClient("minerIdClient"));
          }
          throw new Exception($"Invalid configuration - either {nameof(appSettings.MinerIdServer)} or {nameof(appSettings.WifPrivateKey)} are required.");
        }
      );

      if (HostEnvironment.EnvironmentName != "Testing")
      {
        services.AddHostedService<StartupChecker>();
        services.AddTransient<IClock, Clock>();
        services.AddHostedService<CleanUpTxHandler>();
      }
      else
      {
        // We register clock as singleton, so that we can set time in individual tests
        services.AddSingleton<IClock, MockedClock>();
      }

      services.AddHostedService<NotificationService>();
      services.AddHostedService(p => (BlockParser)p.GetRequiredService<IBlockParser>());
      services.AddHostedService<InvalidTxHandler>();


      services.AddSingleton<IPostConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerOptions>();

      services.AddSingleton<ZMQSubscriptionService>();
      services.AddHostedService(p => p.GetService<ZMQSubscriptionService>());

      services.AddSingleton<IdentityProviderStore>();
      services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
          {
            options.RefreshOnIssuerKeyNotFound = false;
            // We validate audience and issuer through IdentityProviders
            options.TokenValidationParameters.ValidateAudience = false;
            options.TokenValidationParameters.ValidateIssuer = false;
            // The rest of the options are configured in ConfigureJwtBearerOptions
          }
        );

      services.AddCors(options =>
      {
        options.AddDefaultPolicy(
            builder =>
            {
              builder.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
            });
      });

      services.AddSwaggerGen(c =>
      {
        c.SwaggerDoc(SwaggerGroup.API, new OpenApiInfo { Title = "Merchant API", Version = Const.MERCHANT_API_VERSION });
        c.SwaggerDoc(SwaggerGroup.Admin, new OpenApiInfo { Title = "Merchant API Admin", Version = Const.MERCHANT_API_VERSION });
        c.ResolveConflictingActions(apiDescriptions => apiDescriptions.First());

        // Add MAPI authorization options
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
          In = ParameterLocation.Header,
          Description = "Please enter JWT with Bearer needed to access MAPI into field. Authorization: Bearer JWT",
          Name = "Authorization",
          Type = SecuritySchemeType.ApiKey
        });

        c.AddSecurityRequirement(new OpenApiSecurityRequirement {
          {
            new OpenApiSecurityScheme
            {
              Reference = new OpenApiReference
              {
                Type = ReferenceType.SecurityScheme,
                Id = "Bearer"
              }
            },
            new string[] { }
          }
        });

        // Add Admin authorization options.
        c.AddSecurityDefinition(ApiKeyAuthenticationHandler.ApiKeyHeaderName, new OpenApiSecurityScheme
        {
          Description = @"Please enter API key needed to access admin endpoints into field. Api-Key: My_API_Key",
          In = ParameterLocation.Header,
          Name = ApiKeyAuthenticationHandler.ApiKeyHeaderName,
          Type = SecuritySchemeType.ApiKey,
        });

        c.AddSecurityRequirement(new OpenApiSecurityRequirement {
          {
            new OpenApiSecurityScheme
            {
              Name = ApiKeyAuthenticationHandler.ApiKeyHeaderName,
              Type = SecuritySchemeType.ApiKey,
              In = ParameterLocation.Header,
              Reference = new OpenApiReference
              {
                Type = ReferenceType.SecurityScheme,
                Id = ApiKeyAuthenticationHandler.ApiKeyHeaderName
              },
            },
            new string[] {}
          }
        });

        // Set the comments path for the Swagger JSON and UI.
        var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = System.IO.Path.Combine(System.AppContext.BaseDirectory, xmlFile);
        c.IncludeXmlComments(xmlPath);
      });
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
      if (env.IsDevelopment())
      {
        app.UseExceptionHandler("/error-development");
      }
      else
      {
        app.UseExceptionHandler("/error");
      }

      app.Use(async (context, next) =>
      {
        // Prevent sensitive information from being cached.
        context.Response.Headers.Add("cache-control", "no-store");
        // To protect against drag-and-drop style clickjacking attacks.
        context.Response.Headers.Add("Content-Security-Policy", "frame-ancestors 'none'");
        // To prevent browsers from performing MIME sniffing, and inappropriately interpreting responses as HTML.
        context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
        // To protect against drag-and-drop style clickjacking attacks.
        context.Response.Headers.Add("X-Frame-Options", "DENY");
        // To require connections over HTTPS and to protect against spoofed certificates.
        context.Response.Headers.Add("Strict-Transport-Security", "max-age=63072000; includeSubDomains; preload");
        await next();
      });

      app.UseHttpsRedirection();

      app.UseSwagger();
      app.UseSwaggerUI(c =>
      {
        c.SwaggerEndpoint($"/swagger/{SwaggerGroup.API}/swagger.json", "Merchant API");
        c.SwaggerEndpoint($"/swagger/{SwaggerGroup.Admin}/swagger.json", "Merchant API Admin");
      });

      app.UseRouting();
      app.UseCors();

      app.UseAuthentication();
      app.UseAuthorization();
      
      app.UseEndpoints(endpoints =>
      {
        endpoints.MapControllers();
      });
    }
  }
}
