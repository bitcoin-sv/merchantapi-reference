// Copyright (c) 2020 Bitcoin Association

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using MerchantAPI.APIGateway.Domain;
using MerchantAPI.APIGateway.Domain.Actions;
using MerchantAPI.APIGateway.Domain.Models;
using MerchantAPI.APIGateway.Domain.Models.Events;
using MerchantAPI.APIGateway.Domain.Repositories;
using MerchantAPI.APIGateway.Infrastructure.Repositories;
using MerchantAPI.APIGateway.Rest.Authentication;
using MerchantAPI.APIGateway.Test.Functional.CallBackWebServer;
using MerchantAPI.APIGateway.Test.Functional.Mock;
using MerchantAPI.APIGateway.Test.Functional.Server;
using MerchantAPI.Common.BitcoinRpc;
using MerchantAPI.Common.Clock;
using MerchantAPI.Common.EventBus;
using MerchantAPI.Common.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NBitcoin;
using Npgsql.Logging;

namespace MerchantAPI.APIGateway.Test.Functional
{
  public class TestBase
  {
    public AppSettings AppSettings;
    public IConfigurationRoot Configuration { get; private set; }

    public TxRepositoryPostgres TxRepositoryPostgres { get; private set; }
    public NodeRepositoryPostgres NodeRepository { get; private set; }
    public FeeQuoteRepositoryPostgres FeeQuoteRepository { get; private set; }

    public IMinerId MinerId { get; private set; }

    public IBlockChainInfo BlockChainInfo { get; private set; }

    public INodes Nodes { get; private set;  }
    
    public IRpcMultiClient rpcMultiClient { get; private set; }

    public IBlockChainInfo blockChainInfo { get; private set; }

    public IEventBus eventBus { get; private set; }

    protected Microsoft.AspNetCore.TestHost.TestServer server;
    protected HttpClient client;

    protected Microsoft.AspNetCore.TestHost.TestServer serverCallBack;
    protected HttpClient clientCallBack;


    protected ILogger loggerTest;
    protected ILoggerFactory loggerFactory;

    // Mocks are non-null only when we actually use the,
    protected FeeQuoteRepositoryMock feeQuoteRepositoryMock;
    protected RpcClientFactoryMock rpcClientFactoryMock;

    public const string LOG_CATEGORY = "MerchantAPI.APIGateway.Test.Functional";

    private static bool providerSet;
    private static double quoteExpiryMinutes = 10;
    private readonly string dbConnectionString;

    public static AutoResetEvent SyncTest = new AutoResetEvent(true);

    public CallBackFunctionalTests CallBack = new CallBackFunctionalTests();

    protected UserAndIssuer GetMockedIdentity
    {
      get
      {
        return new UserAndIssuer() { Identity = "5", IdentityProvider = "http://mysite.com" };
      }
   }

    public const string genesisBlock =
      "0100000000000000000000000000000000000000000000000000000000000000000000003ba3edfd7a7b12b27ac72c3e67768f617fc81bc3888a51323a9fb8aa4b1e5e4a29ab5f49ffff001d1dac2b7c0101000000010000000000000000000000000000000000000000000000000000000000000000ffffffff4d04ffff001d0104455468652054696d65732030332f4a616e2f32303039204368616e63656c6c6f72206f6e206272696e6b206f66207365636f6e64206261696c6f757420666f722062616e6b73ffffffff0100f2052a01000000434104678afdb0fe5548271967f1a67130b7105cd6a828e03909a67962e0ea1f61deb649f6bc3f4cef38c4f35504e51ec112de5c384df7ba0b8d578a4c702b6bf11d5fac00000000";



    // Transaction dependency:
    //
    //   txC0 -> txC1 -> txC2 -> txC3
    //
    //   tx2Input1Hex --\  
    //   tx2Input2Hex ----> tx2Hex
    //
    //   txZeroFeeInput1Hex --\  
    //   txZeroFeeInput2Hex ----> txZeroFeeHex
    //


    public const string txC0Hash = "19e8d6172493b899bdadfd1e012e042a708b0844b388a18f6903586e9747a709";

    public const string txC0Hex =
      "0100000001878e9e24013c7f44d72dcd23d13cb542810742763c73d18fe4e66041541d2fc4000000006c493046022100bc9f9757625cb05fec40372e8df159c95a5a6984e0d9b915f059c4956e9a4e6d022100edfc8f5a508a255123218d5930c4a3180df4bd859c9fd57b0dc77c51bf154268012102ab4e425640983e035661216fec925e1c7cb6fceccf112a9aaeec1b1f75d69f9cffffffff02b00f6f06000000001976a9144f416f5f3049dd13b6a1b479b7d80e7d17e0f29788ac80f0fa02000000001976a91470b71d2a9295048934246c7e6678fa2838edd53688ac00000000";
    public const string txC1Hash = "9ce8e56fc0ab1b673b4fb7a092b805414685140be9bb70e3f8a2eb3d4f9c1105";

    public const string txC1Hex =
      "010000000109a747976e5803698fa188b344088b702a042e011efdadbd99b8932417d6e819000000006b48304502200bd4907de00d13a86aad4fe5f44796e3234ed7af7bc8d21a6da142e956c906160221008dc182f5f7df8ff64edde362db47c0819492ff4b4f95bb9b9607c293ec3f6843012102a4eea0a9ceb6f859a59268e050ac9212362b13d65dd0f53f27ce1de7cfd037a3ffffffff02409e9903000000001976a914326ed7cabfe3e5e037541b9c1f45e7b6aed0d71088ac7071d502000000001976a914d6e343ca15707dedfdfdb59303901a79eccead9788ac00000000";

    public const string txC2Hash = "a07dbd3c8165add674edb68bf9c8b48272135b6a35b9dddd117d00a52995347c";
    public const string txC2Hex =
      "010000000105119c4f3deba2f8e370bbe90b1485464105b892a0b74f3b671babc06fe5e89c000000006a47304402201efd88a86fc6de114b0a55e75d48a653eedb03281a3f2b52950e8377ed0ddbbf022076e650e384620d0f32d8f25107c9e9e2e3325c2bd7ad339e92408111781bb2e00121030984e003984bfc084a7b8fe7adec740e47b3f48d0e28d1391846b2e9e6b248a2ffffffff0240443701000000001976a9141dbc11836e98dccd15bc092d88ff611015c41d0a88ac005a6202000000001976a91461f8d0abc4c919b0693030734f4d9d3ce424fef988ac00000000";


    public const string txC3Hash = "3c412d497cb5d83fff8270062e9fe6c1fba147eed156887081dddfcc117e854c";

    public const string txC3Hex =
      "01000000017c349529a5007d11ddddb9356a5b137282b4c8f98bb6ed74d6ad65813cbd7da0010000006b483045022100e0a4fb47b9ff8cab51bac9904a7462ea063d4ab588e197231b03cd699d79990602205b9beb6a31ade571f021a117a0bcd1afbc63a786ffc61af898abce402d84c1520121022cd68b60621f51af57f1c87e52e1b1f394584273d104dff2c2b80329115c39b0ffffffff0240420f00000000001976a914cc7ab903497dc6326c5e5135bba23f1a4653db2388acb0f05202000000001976a91461f8d0abc4c919b0693030734f4d9d3ce424fef988ac00000000";

    //public const string txC2Input1Hex = tx0Hex;
      

    public const string txZeroFeeHash = "54dc90aa618ea1c300aac021399c66f5f5152848a57984a757075036e3046147";

    public const string txZeroFeeHex =
      "0100000002af0bf9c887049d8a143cff21d9e10d921ab39a3645c0531ba192291b7793c6f8100000008b483045022100904a2e0e8f597fc1cc271b6294b097a6edc952e30c453e3530f92492749769a8022018464c225b03c28791af06bc7fed129dcaaeff9ec8135ada1fb11762ce081ea9014104da289192b0845d5b89ce82665d88ac89d757cfc5fd997b1de8ae47f7780ce6a32207583b7458d1d2f3fd6b3a3b842aea9eb789e2bea57b03d40e684d8e1e0569ffffffff0d088b85950cf484bbcd1114c8fd8ad2850dcf2784c0bbcff9af2b3377211de5010000008b4830450220369df7d42795239eabf9d41aee75e3ff20521754522bd067890f8eedf6044c6d0221009acfbd88d51d842db87ab990a48bed12b1f816e95502d0198ed080de456a988d014104e0ec988a679936cea80a88e6063d62dc85182e548a535faecd6e569fb565633de5b4e83d5a11fbad8b01908ce71e0374b006d84694b06f10bdc153ca58a53f87ffffffff02f6891b71010000001976a914764b8c407b9b05cf35e9346f70985945507fa83a88acc0dd9107000000001976a9141d1310fe87b53fec8dbc8911f0ebc112570e34b288ac00000000";

    public const string txZeroFeeInput1Hex =
      "0100000001d69e69b154ca77e77fcc6448e5c2355d316ebdf61a75985b0b01e4c3a1148ccc020000006b48304502210097c42831910fd23395d071304c54cd766bca1244040e7f2bb56e3b9859af35bb02202bbb610f6b74ae513c0b9edf23d33f3b733a5cf43cb8fec59b495fa4cbcb54e7012103157409e2e1a7f1a7dbf0c66fb801198048a95be85741fd91156b298632ded36bffffffff222abb3e18000000001976a91489612876c74a84efa48846bcf4197aae6557605188ac7d3f150c000000001976a914bde76fa2e8ae813e8055783de08b6b6fea7044be88acc0dd6c09000000001976a91416b82ea6b7092c5ff6c68cf58a4ae3e7c8bf33ba88ac1c419107000000001976a914a5eb2f54c79bc5a68f957389b129834e4cdb64d288ac7950f106000000001976a9141b2eeddf22e55c20d5b3530aefd113e66dfba6df88ac540fe606000000001976a914439cc8affed4b673ea34cf2b8a2ba48dae90b10f88ac2f5c0e06000000001976a9149afcaf3dc53b98caa7c4d3bfaaf1c13329ba7b2c88ac0e7d0106000000001976a914d6af331c8a8f36342212de9a1df8db88cf2c0e0e88acea81b127000000001976a9144a584acc9c92803fd7f82950e4f00fe0b655523b88ac0931f905000000001976a9144c5beebd44deb42029a0d430de4dcfcc6e91b19b88aca4eff404000000001976a914b633d95c1e3b98e2992b4895c699c1637c4120e188ac3c08b304000000001976a914342a1e281d820812c184643e144de4969730f63088ac48a0dc03000000001976a91464cb0321b68cb20ee3dce19ce74db29f1d05fb6288acb5e14a03000000001976a914016aed0e4c6cf016197e8126c77668b651f8209988ac21a80303000000001976a91461348b6877403e20add07ae4fa0a66d727dbb66b88acc08b0203000000001976a91472c9d04559086a72a2a0891688f5e14afa2e6d7588ac5ed8fb02000000001976a914afd5fd92bb282f3913c9694a76f727668074be1988acea047e01000000001976a914b8f2e786f3abe68ffbf4fa933fbbd15a9f85599988acc0225e01000000001976a91474c5fc8ba096bd01810f186087f7b3875e27401688ac12035601000000001976a914ffe375d0cf378c47df327c56a90c80c3ba49d25088aca3a9a200000000001976a914a83a4437e6f15dc60bb727e79f5b021a6bbe393288ac74ec8a00000000001976a914206ccface8d12d0b50d2d6cc2868f171555b202988accc6a8700000000001976a914339826a8480ef55d260c3963d24ce4e33e28938988ac94bf6000000000001976a91452de2ec0c71362a88f53a889e04dd626e15c07a988ac90825b00000000001976a914a401372e20926a900c27707f065497fb2f5f2d9a88ac400c4a00000000001976a91418d0d82c93ea9297f2fb02fa8cab811d472f972788ac8fd73800000000001976a914fb12869b702b45059b6b74cdff5ffdc0eb1c578088ac498f2d00000000001976a914ded40bfa4a5529d9d402f4da12916ed3a626705788acdc032a00000000001976a914c24cdeea87904ab53102028eba713d782a75352688acd06e2500000000001976a9142614ace2bcdaa1864db04083f786775604044ce788ac7d562000000000001976a91443b9904d542390283a60d7ab729ec951cb48926788ac9f492000000000001976a914ca3a1ca2acffd98a0cdaac031f5d01e2b8b2892688ac60941f00000000001976a914a122aab55e2712002ab9655f82a27c6453ee1af388acab661f00000000001976a914058e25fc2c5ab11eb92a92a5d081365dd31d802788ac00000000";

    public const string txZeroFeeInput2Hex =
      "01000000049efc5a3666e65d55b08f9408581045a2edb5d616cef087f2fba4a46a94bc42f9000000008b48304502210088925c3e614fd8c8bd96d40bd5c31b7febfbcec49e4caad97518243ecfa0e2c9022025426677c6dbc0d28b9cb79f41b1b2e6c87d512edc8f4e0ac3083f94cc58540e0141048409cd22897ea5de274292cc506f0ccdbd9efb2f67a05ae8011db4a9a6b876302c0714986d8137040bc3af3cc1b791ab28f9ecce6ab83a03e41e0ab6af7bf3c2ffffffff6bd183d94dd48c907fdf3b4af9ce70aad3ae2585c94fba1473e493eec6bb4ab1010000008a4730440220439199eb00d37eaee9fee0c7fa611f6cbc1d8889e331baaa18b4c13d5adcd45102201941e7e2a7e5521910357752960c6ffcdb3fd8d7a055bb06102869552e839f1f01410466c15f02b5f2999d09a6bc0ea6c81f6c37e5ce20e816bb7b40e458a8c17dcfdfcc0ec2e5d4d56267ac4433c70cfa514bdec7060ce2aec0fd47484365e0a8d2d7ffffffffba4244c6968ad5325e2549a7a4bca7db5ef3e68c2bb78e137d8ddb5e5bf691b2000000008b48304502210086619dc951e6bac73d4d529ec0574a534e916f8ff913a3db8a45f938e2b10da302205e22b13bb7e9b81fea355cb0db0bcd9268ec9612762efab440aff3846aaf7d9c014104d4a7ae83f4bed585020eebb8a20bdaf1442d06da1121599db1a78580eda8d9aefab6ad0330e1cef62dd87c8967e041b945ca03b5c93127aa5a7737a05707f97fffffffff2c9301ba9a2866ce8230544f3464ba73f1f7e9b631ebf3fcf24308eb4883ce55010000008b483045022100ea511faf732eab56a4fedaff769a6e0ed44b95ae5ac1f1ef67c0d6ea752596cf022063e3789d86c872771165999ede7e0b431cf3bf1c3b218c58a083fc9d325efd4c014104265113069f7344e00b3443226fc86d67262a20ffaa4a9dbe43d13fc3df883d184190d3577bb109d997c512aff2c7bf2d63897f63bfea9549273b2e1028a9963cffffffff02c8ac31a4000000001976a9145fed6d88571b0e29a5ba943b007ef372989f18a588ac588fb175010000001976a914f915fe8afbacd5ee8e2292320757959e33d0d75088ac00000000";

    public const string tx2Hash = "7fb40b4c18ca267aa5ab3a042ef8608c1f90e73ff171ce876c2d2242ed79c65d";

    public const string tx2Hex =
      "0100000002e0f54217184155930762c85c6a6b5d6367fbfc957e4cff402608dac63736d2df000000008c493046022100bbd17c19404a2b3697696786af7a9252e2e18db8db68b94221b2d1ac0a6b55d2022100fe5cfddffba44766b2c2278bf7e5ad55091800e004261c08df40ac40c6b5f655014104642784e1a9b7e79e3c78836c6a2ebbfaf0afdabe1a4c8b6a7e8461c71e32689d34b7113cad454cd27dc89a010b3631c653f1c3c596109d32bbdff3572babce5bffffffffece42b464083556bf807ffcc7fddeab601ac9d6628b536067a7e73f1d5489dac010000008b48304502203c5d5d0d395a58f38ec80357ae55d705cf252523a9a164f49cce53b171a15ecb022100aa6067651eb13ea4bd27dae43b7dec1596b42795e3e42d05d6c993b34d8d23cc014104642784e1a9b7e79e3c78836c6a2ebbfaf0afdabe1a4c8b6a7e8461c71e32689d34b7113cad454cd27dc89a010b3631c653f1c3c596109d32bbdff3572babce5bffffffff02fcb81300000000001976a914cfb434ad9fc1aa6bb9ae4656c321ef20b91645e888ac00e1f505000000001976a914825072b41ba1d15f68dd50e5b493c8d00eb26bd988ac00000000";

    public const string tx2Input1Hex =
      "0100000002066a574f3dc707e3a151e274c3ee0747a26faf3fffcfbdd64c3a6362d36e9611010000008a47304402201e62db8fb7c078ee42c32783b52f932a50ac331edab514a9a8b17aa6277f2ec8022045dedc82ea9a407e4ac3a41c52459c482947c44e53e09f7cd7bbd5c32339aec5014104c833003b02a761088c216bb75518fb1b553449ed3b1902a1fe45167e7f50bedc996bd8f1b0937adfea669bc7a3fa3c474a1a5c6a17318ada51ed8e673677bc9bffffffffb8703533b79e4992b6007a396c83907a0da5d0a080948ed2bae0d75982964817740000008c493046022100e63ea3884d9061540f2802be9e07566b4fea2fb0df294a4c53f66d9a4f9da3fd022100e0c892cef742ddb7b8168f293a008bb512289bf112d993f57820b3294f838638014104ac7cdbe24e39f572e938511aeae7ab5a4775d9e972707c8791b45761d61409d846bdcb4e2f8d323e37ca70274505323c179203e6112faa2510a29e0a02e0f735ffffffff026c058b03000000001976a914726ab8a7af161b94c211e60175f1ca3f76fd389588ac89546c06000000001976a914652b37c2942d901b8548b49d942358fb737823f488ac00000000";

    public const string tx2Input2Hex =
      "01000000012a5fe7ae3e0db0eea0d69b492887809e13c7f03aed17252a2281527dc4797afe000000008c493046022100c6ae26aa8435ddbc4aac5d2dd26326e81c85049f4a2893553c86a72af2aac79c022100971ca28c5e5379b49b6aacef1453e76dede1a03d52887688b23c5739683f6f470141043f6e437156c7380266db90a3d35cb0d6e0bfd062fd65481f2114bd0baf8e834273e4c4e52e274aa7a96e861169a0a150d4e723191a873cf68c29d20e9624b739ffffffff0222e40c1d010000001976a9143310b7c20896ca4082b08eaaaa5791ccf18ed36188ace0577f02000000001976a914726ab8a7af161b94c211e60175f1ca3f76fd389588ac00000000";



    public async Task WaitUntilAsync(Func<bool> predicate, int timeOutSeconds = 10)
    {
      var start = DateTime.UtcNow;

      while ((DateTime.UtcNow - start).TotalSeconds < timeOutSeconds)
      {
        if (predicate())
        {
          return;
        }

        await Task.Delay(1000);
      }
      // Output log entry before throwing to make it easier to pinpoint exact position of failure in log file
      loggerTest.LogError("WaitUntilAsync: condition was not satisfied in prescribed period - will throw.");
      throw new Exception("WaitUntilAsync: timeout expired. The conditions was not satisfied in prescribed period.");
    }


    public async Task WaitUntilAsync(Func<Task<bool>> predicate, int timeOutSeconds = 10)
    {
      var start = DateTime.UtcNow;

      while ((DateTime.UtcNow - start).TotalSeconds < timeOutSeconds)
      {
        if (await predicate())
        {
          return;
        }

        await Task.Delay(1000);
      }

      throw new Exception("WaitUntilAsync: timeout expired. The conditions was not satisfied in prescribed period.");
    }

    public void  WaitUntilEventBusIsIdle()
    {
      eventBus.WaitForIdle();
      loggerTest.LogInformation("Waiting for the EventBus to become idle completed");
    }
    public static void RepeatUntilException(Action action, int timeOutSeconds = 10)
    {
      var start = DateTime.UtcNow;
      while ((DateTime.UtcNow - start).TotalSeconds < timeOutSeconds)
      {
        try
        {
          action(); //this could take more than timeOutSeconds - we currently do not use cancellation tokens
        }
        catch 
        {
          return;
        }
        Thread.Sleep(100);
      }
      throw new Exception("RepeatUntilException: timeout expired. The exception did not occur in prescribed period.");
    }
    public TestBase()
    {
      if (!providerSet)
      {
        // uncomment if needed
        NpgsqlLogManager.Provider = new ConsoleLoggingProvider(NpgsqlLogLevel.Debug);
        NpgsqlLogManager.IsParameterLoggingEnabled = true;
        providerSet = true;
      }

      SqlMapper.AddTypeHandler(new Common.DateTimeHandler());

      string appPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
      Configuration = new ConfigurationBuilder()
        .AddJsonFile(Path.Combine(appPath, "appsettings.json"))
        .AddJsonFile(Path.Combine(appPath, "appsettings.development.json"), optional: true)
        .AddJsonFile(Path.Combine(appPath, "appsettings.test.functional.development.json"), optional: true)
        .AddEnvironmentVariables()
        .Build();


      AppSettings = Configuration.GetSection("AppSettings").Get<AppSettings>();

      dbConnectionString = Configuration["ConnectionStrings:DBConnectionString"];
    }

   
    public void Initialize(bool mockedServices = false)
    {
      SyncTest.WaitOne(); // tests must not run in parallel since each test first deletes database
      try
      {


        // setup call back server
        serverCallBack = CallBackServer.GetTestServer(CallBack.Url, CallBack);
        clientCallBack = serverCallBack.CreateClient();

        //setup server
        server = TestServerBase.CreateServer(mockedServices, serverCallBack, dbConnectionString);
        client = server.CreateClient();

        // setup repositories
        NodeRepository = server.Services.GetRequiredService<INodeRepository>() as NodeRepositoryPostgres;
        TxRepositoryPostgres = server.Services.GetRequiredService<ITxRepository>() as TxRepositoryPostgres;
        FeeQuoteRepository = server.Services.GetRequiredService<IFeeQuoteRepository>() as FeeQuoteRepositoryPostgres;

        BlockChainInfo = server.Services.GetRequiredService<IBlockChainInfo>();
        MinerId = server.Services.GetRequiredService<IMinerId>();

        // setup common services
        loggerFactory = server.Services.GetRequiredService<ILoggerFactory>();
        loggerTest = loggerFactory.CreateLogger(LOG_CATEGORY);

        Nodes = server.Services.GetRequiredService<INodes>();
        blockChainInfo = server.Services.GetRequiredService<IBlockChainInfo>();
        rpcMultiClient = server.Services.GetRequiredService<IRpcMultiClient>();

        eventBus = server.Services.GetRequiredService<IEventBus>();

        rpcClientFactoryMock = server.Services.GetRequiredService<IRpcClientFactory>() as RpcClientFactoryMock;
        feeQuoteRepositoryMock = server.Services.GetRequiredService<IFeeQuoteRepository>() as FeeQuoteRepositoryMock;
        FeeQuoteRepositoryMock.quoteExpiryMinutes = quoteExpiryMinutes;

        if (rpcClientFactoryMock != null)
        {
          rpcClientFactoryMock.SetUpTransaction(
            txC3Hex,
            txC2Hex,
            txZeroFeeHex,
            txZeroFeeInput1Hex,
            txZeroFeeInput2Hex,
            tx2Hex,
            tx2Input1Hex,
            tx2Input2Hex);

          rpcClientFactoryMock.AddKnownBlock(0, HelperTools.HexStringToByteArray(genesisBlock));

          rpcClientFactoryMock.Reset(); // remove calls that are used to test node connection when adding a new node
        }
        loggerTest.LogInformation($"Path: {Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)}");
        loggerTest.LogInformation($"ConnString: {dbConnectionString}");
      }
      catch
      {
        SyncTest.Reset();
        // If there was error during initialization, let the other tests run (although they will probably also fail)
        throw;
      }

    }

    public void Cleanup(Action afterServerDisposed = null)
    {
      loggerTest?.LogInformation("Starting test cleanup");
      server?.Dispose();
      loggerTest?.LogInformation("TestServer disposed");

      afterServerDisposed?.Invoke();
      SyncTest.Set();
      loggerTest?.LogInformation("Test cleanup finished");
    }


    // Helper Json related functions
    public async Task<(TResponse response, HttpResponseMessage httpResponse)> Post<TRequest, TResponse>(
      string baseUrl,
      HttpClient client,
      TRequest request,
      HttpStatusCode expectedStatusCode)
      where TResponse : class
    {
      return await Post<TResponse>(
        baseUrl,
        client,
        new StringContent(JsonSerializer.Serialize(request),
          Encoding.UTF8, "application/json"),
        expectedStatusCode
      );
    }


    /// <summary>
    /// When set it is used as Rest authentication header in tests
    /// </summary>
    public string RestAuthentication { get; set; }

    /// <summary>
    /// When set it is used as API Key Rest authentication header in tests
    /// </summary>
    public string ApiKeyAuthentication { get; set; }


    public async Task<(TResponse response, HttpResponseMessage httpResponse)> ParseResponse<TResponse>(HttpResponseMessage httpResponse, HttpStatusCode expectedStatusCode)  where TResponse: class
    {

      if (expectedStatusCode != httpResponse.StatusCode)
      {
        // include body in assert message to make debugging easier
        var body = await httpResponse.Content.ReadAsStringAsync();
        Assert.AreEqual(expectedStatusCode, httpResponse.StatusCode, "body: " + body);
      }


      string responseString = await httpResponse.Content.ReadAsStringAsync();
      TResponse response = null;

      if (httpResponse.IsSuccessStatusCode) // Only try to deserialize in case there are no exception
      {
        response = JsonSerializer.Deserialize<TResponse>(responseString);
      }

      return (response, httpResponse);
    }

    HttpRequestMessage BuildHttpRequestMessage(HttpMethod httpMethod, string url, HttpContent content = null)
    {
      var requestMessage = new HttpRequestMessage(httpMethod, url)
      {
        Content = content
      };

      if (!string.IsNullOrEmpty(RestAuthentication))
      {
        requestMessage.Headers.Add("Authorization", RestAuthentication);
      }
      if (!string.IsNullOrEmpty(ApiKeyAuthentication))
      {
        requestMessage.Headers.Add(ApiKeyAuthenticationHandler.ApiKeyHeaderName, ApiKeyAuthentication);
      }

      return requestMessage;

    }
    public async Task<(TResponse response, HttpResponseMessage httpResponse)> Get<TResponse>(
      string url,
      HttpClient client,
      HttpStatusCode expectedStatusCode)
      where TResponse : class
    {
      var httpResponse = await client.SendAsync(BuildHttpRequestMessage(HttpMethod.Get, url));

      return await ParseResponse<TResponse>(httpResponse, expectedStatusCode);
    }

    public async Task<(TResponse response, HttpResponseMessage httpResponse)> Post<TResponse>(
      string url,
      HttpClient client,
      ByteArrayContent requestContent,
      HttpStatusCode expectedStatusCode)
      where TResponse : class
    {
      
      var httpResponse = await  client.SendAsync(BuildHttpRequestMessage(HttpMethod.Post, url, requestContent));

      return await ParseResponse<TResponse>(httpResponse, expectedStatusCode);
    }

    public Tx CreateNewTx(string txHash, string txHex, bool merkleProof, bool dsCheck)
    {
      var tx = new Tx
      {
        DSCheck = dsCheck,
        MerkleProof = merkleProof,
        ReceivedAt = MockedClock.UtcNow,
        TxExternalId = new uint256(txHash),
        TxPayload = HelperTools.HexStringToByteArray(txHex)
      };
      var transaction = HelperTools.ParseBytesToTransaction(tx.TxPayload);
      tx.TxIn = transaction.Inputs.AsIndexedInputs().Select(x => new TxInput
      {
        N = x.Index,
        PrevN = x.PrevOut.N,
        PrevTxId = x.PrevOut.Hash.ToBytes()
      }).ToList();

      return tx;
    }


    public async Task<long> CreateAndPublishNewBlock(IRpcClient rpcClient, long? blockHeightToStartFork, params Transaction[] transactions)
    {
      long blockCount = await rpcClient.GetBlockCountAsync();
      if (blockCount == 0)
      {
        var blockHex = await rpcClient.GetBlockAsBytesAsync(await rpcClient.GetBestBlockHashAsync());
        var firstBlock = NBitcoin.Block.Load(blockHex, Network.Main);
        rpcClientFactoryMock.AddKnownBlock(blockCount, firstBlock.ToBytes());
        PublishBlockHashToEventBus(await rpcClient.GetBestBlockHashAsync());
      }
      PubKey pubKey = new Key().PubKey;

      foreach (var tx in transactions)
      {
        NBitcoin.Block lastBlock;
        if (blockHeightToStartFork.HasValue)
        {
          lastBlock = NBitcoin.Block.Load(await rpcClient.GetBlockByHeightAsBytesAsync(blockHeightToStartFork.Value), Network.Main);
        }
        else
        {
          lastBlock = NBitcoin.Block.Load(await rpcClient.GetBlockAsBytesAsync(await rpcClient.GetBestBlockHashAsync()), Network.Main);
        }
        var block = lastBlock.CreateNextBlockWithCoinbase(pubKey, new Money(50, MoneyUnit.MilliBTC), new ConsensusFactory());
        block.AddTransaction(tx);
        block.Check();
        if (blockHeightToStartFork.HasValue)
        {
          blockCount = blockHeightToStartFork.Value;
        }

        rpcClientFactoryMock.AddKnownBlock(++blockCount, block.ToBytes());
        PublishBlockHashToEventBus(block.GetHash().ToString());
      }

      return blockCount;
    }

    protected void PublishBlockHashToEventBus(string blockHash)
    {
      eventBus.Publish(new NewBlockDiscoveredEvent()
      {
        CreationDate = MockedClock.UtcNow,
        BlockHash = blockHash
      });

      WaitUntilEventBusIsIdle();
    }


    protected void InsertFeeQuote()
    {

      using (MockedClock.NowIs(DateTime.UtcNow.AddMinutes(-1)))
      {
        var feeQuote = new FeeQuote
        {
          Id = 1,
          CreatedAt = MockedClock.UtcNow,
          ValidFrom = MockedClock.UtcNow,
          Fees = new[] {
              new Fee {
                FeeType ="standard",
                MiningFee = new FeeAmount {
                  Satoshis = 500,
                  Bytes = 1000,
                  FeeAmountType = FeeAmount.AmountType.MiningFee
                },
                RelayFee = new FeeAmount {
                  Satoshis = 250,
                  Bytes = 1000,
                  FeeAmountType = FeeAmount.AmountType.RelayFee
                },
              },
              new Fee {
                FeeType ="data",
                MiningFee = new FeeAmount {
                  Satoshis = 500,
                  Bytes = 1000,
                  FeeAmountType = FeeAmount.AmountType.MiningFee
                },
                RelayFee = new FeeAmount {
                  Satoshis = 250,
                  Bytes = 1000,
                  FeeAmountType = FeeAmount.AmountType.RelayFee
                },
              },
          }
        };
        if (FeeQuoteRepository.InsertFeeQuoteAsync(feeQuote).Result == null)
        {
          throw new Exception("Can not insert test fee quote");
        }
      }
    }

    public async Task WaitForEventBusEventAsync<T>(EventBusSubscription<T> subscription, string description, Func<T, bool> predicate) where T : IntegrationEvent
    {
      try
      {

        do
        {
          var evt = await subscription.ReadAsync(new CancellationTokenSource(5_000).Token);

          if (predicate(evt))
          {
            break;
          }

          loggerTest.LogInformation($"Got notification event {evt}, but is not the one that we are looking for");
        } while (true);
      }
      catch (OperationCanceledException)
      {
        string msg = $"Timeout out while waiting for integration event {typeof(T).Name}. {description}";
        loggerTest.LogInformation(msg);
        throw new Exception(msg);
      }

      loggerTest.LogInformation($"The following wait for event completed successfully: {description}");
    }

  }
}
