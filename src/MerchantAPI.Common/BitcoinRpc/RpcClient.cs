// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MerchantAPI.Common.BitcoinRpc.Responses;
using MerchantAPI.Common.Json;
using Microsoft.Extensions.Logging;

namespace MerchantAPI.Common.BitcoinRpc
{
  public class RpcClient : IRpcClient
  {
    readonly Uri Address;
    readonly NetworkCredential Credentials;
    ILogger<RpcClient> logger;

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(100);
    public int NumOfRetries { get; set; } = 50;

    private static Lazy<HttpClient> SharedHttpClient = new Lazy<HttpClient>(() => new HttpClient() { Timeout = Timeout.InfiniteTimeSpan }); // intended to be instantiated once : ref docs.microsoft.com

    public HttpClient HttpClient { get; set; }

    public RpcClient(Uri address, NetworkCredential credentials, ILogger<RpcClient> logger, HttpClient httpClient)
    {
      Address = address;
      Credentials = credentials;
      this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
      HttpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<long> GetBlockCountAsync(CancellationToken? token = null)
    {
      return await RequestAsyncWithRetry<long>(token, "getblockcount");
    }

    public async Task<RpcGetBlockWithTxIds> GetBlockWithTxIdsAsync(string blockHash, CancellationToken? token = null)
    {
      return await RequestAsyncWithRetry<RpcGetBlockWithTxIds>(token, "getblock", null, blockHash, 1);

    }

    public async Task<RpcGetBlock> GetBlockAsync(string blockHash, int verbosity, CancellationToken? token = null)
    {
      if (verbosity == 0 || verbosity == 1)
      {
        throw new Exception("GetBlockAsync method does not accept verbosity level 0, 1.");
      }
      return await RequestAsyncWithRetry<RpcGetBlock>(token, "getblock", null, blockHash, verbosity);
    }

    public async Task<RpcBitcoinStreamReader> GetBlockAsStreamAsync(string blockHash, CancellationToken? token = null)
    {
      return await RequestAsyncWithRetry<RpcBitcoinStreamReader>(token, "getblock", null, blockHash, 0 );
    }

    public async Task<byte[]> GetBlockByHeightAsBytesAsync(long blockHeight, CancellationToken? token = null)
    {
      var response = await RequestAsyncWithRetry<string>(token, "getblockbyheight", null, blockHeight, 0);
      return HelperTools.HexStringToByteArray(response);
    }

    public async Task<string> GetBlockHashAsync(long height, CancellationToken? token = null)
    {
      return await RequestAsyncWithRetry<string>(token, "getblockhash", null, height);
    }

    public async Task<RpcGetBlockHeader> GetBlockHeaderAsync(string blockHash, CancellationToken? token = null)
    {
      return await RequestAsyncWithRetry<RpcGetBlockHeader>(token, "getblockheader", null, blockHash, true);
    }

    public async Task<string> GetBlockHeaderAsHexAsync(string blockHash, CancellationToken? token = null)
    {
      return await RequestAsyncWithRetry<string>(token, "getblockheader", null, blockHash, false);
    }

    public async Task<RpcGetRawTransaction> GetRawTransactionAsync(string txId, int retryCount, CancellationToken? token = null)
    {
      return await RequestAsyncWithRetry<RpcGetRawTransaction>(token, "getrawtransaction", retryCount, txId, true);
    }

    public async Task<byte[]> GetRawTransactionAsBytesAsync(string txId, CancellationToken? token = null)
    {
      return HelperTools.HexStringToByteArray(await RequestAsyncWithRetry<string>(token, "getrawtransaction", null, txId, false));
    }

    public async Task<string> GetBestBlockHashAsync(CancellationToken? token = null)
    {
      return await RequestAsync<string>(token, "getbestblockhash", null);
    }

    public async Task<RpcGetMerkleProof> GetMerkleProofAsync(string txId, string blockHash, CancellationToken? token = null)
    {
      if (string.IsNullOrEmpty(txId))
      {
        throw new Exception("'txId' parameter must be set in call to GetMerkleProof.");
      }
      return await RequestAsync<RpcGetMerkleProof>(token, "getmerkleproof", new object[] { txId, blockHash });
    }

    public async Task<RpcGetMerkleProof2> GetMerkleProof2Async(string blockHash, string txId, CancellationToken? token = null)
    {
      if (string.IsNullOrEmpty(txId))
      {
        throw new Exception("'txId' parameter must be set in call to GetMerkleProof2.");
      }
      return await RequestAsync<RpcGetMerkleProof2>(token, "getmerkleproof2", new object[] { blockHash, txId, false, "header" });
    }

    public async Task<RpcGetBlockchainInfo> GetBlockchainInfoAsync(CancellationToken? token = null)
    {
      return await RequestAsync<RpcGetBlockchainInfo>(token, "getblockchaininfo", null);
    }

    public async Task<string> SendRawTransactionAsync(byte[] transaction, bool allowhighfees, bool dontCheckFees, CancellationToken? token )
    {

      var rpcResponse = await MakeRequestAsync<string>(token, new RpcRequest(1, "sendrawtransaction", 
        HelperTools.ByteToHexString(transaction),
        allowhighfees,
        dontCheckFees
        ));
      return rpcResponse.Result;
    }

    public async Task<RpcSendTransactions> SendRawTransactionsAsync(
      (byte[] transaction, bool allowhighfees, bool dontCheckFees, bool listUnconfirmedAncestors)[] transactions, CancellationToken? token = null)
    {

      var t = transactions.Select(
        tx => new RpcSendTransactionsRequestOne
        {
          Hex = HelperTools.ByteToHexString(tx.transaction),
          AllowHighFees = tx.allowhighfees,
          DontCheckFee = tx.dontCheckFees,
          ListUnconfirmedAncestors = tx.listUnconfirmedAncestors
        }).Cast<object>().ToArray();

      object param1 = t; // cast to object so that it is not interpreted as multiple arguments
      var rpcResponse = await MakeRequestAsync<RpcSendTransactions>(token, new RpcRequest(1, "sendrawtransactions", param1));
      return rpcResponse.Result;
    }

    public async Task StopAsync(CancellationToken? token = null)
    {
      _ = await RequestAsync<string>(token, "stop", null);
    }

    public Task<string[]> GenerateAsync(int n, CancellationToken? token = null)
    {
      return RequestAsync<string[]>(token, "generate", n);
    }

    public Task<string> SendToAddressAsync(string address, double amount, CancellationToken? token = null)
    {
      return RequestAsync<string>(token, "sendtoaddress",
        address, 
        amount.ToString(CultureInfo.InvariantCulture)
      );
    }

    public Task<RpcActiveZmqNotification[]> ActiveZmqNotificationsAsync(CancellationToken? token = null)
    {
      return RequestAsync<RpcActiveZmqNotification[]>(token, "activezmqnotifications", null);
    }

    public Task<RpcGetNetworkInfo> GetNetworkInfoAsync(CancellationToken? token = null)
    {
      return RequestAsync<RpcGetNetworkInfo>(token, "getnetworkinfo", null);
    }

    public Task<RpcGetTxOuts> GetTxOutsAsync(IEnumerable<(string txId, long N)> outpoints, string[] fieldList, CancellationToken? token = null)
    {
      var param = outpoints.Select(
        x => new GetTxOutsInput
        {
          TxId = x.txId,
          N = x.N
        }).Cast<object>().ToArray();
      return RequestAsync<RpcGetTxOuts>(token, "gettxouts", param, fieldList, true);
    }

    public Task<string> SubmitBlock(byte[] block, CancellationToken? token = null)
    {
      return RequestAsync<string>(token, "submitblock", HelperTools.ByteToHexString(block));
    }

    public  Task<string[]> GetRawMempool(CancellationToken? token = null)
    {
      return RequestAsync<string[]>(token, "getrawmempool");
    }

    public Task<RpcVerifyScriptResponse[]> VerifyScriptAsync(bool stopOnFirstInvalid,
                                                         int totalTimeoutSec,
                                                         IEnumerable<(string Tx, int N)> dsTx,
                                                         CancellationToken? token = null)
    {
      var reqParams = dsTx.Select(
        x => new RpcVerifyScriptRequest
        {
          Tx = x.Tx,
          N = x.N
        }).ToArray();

      return RequestAsync<RpcVerifyScriptResponse[]>(
          token,
          "verifyscript",
          reqParams, stopOnFirstInvalid, totalTimeoutSec);
    }

    private async Task<T> RequestAsync<T>(CancellationToken? token, string method, params object[] parameters)
    {
      var rpcResponse = await MakeRequestAsync<T>(token, new RpcRequest(1, method, parameters));
      if (rpcResponse.Error != null)
      {
        throw new RpcException(rpcResponse.Error.code, rpcResponse.Error.message, Address.AbsoluteUri);
      }
      
      return rpcResponse.Result;
    }


    private const int waitBetweenRetriesMs = 100;

    private async Task<T> RequestAsyncWithRetry<T>(CancellationToken? token, string method, int? retryCount = null, params object[] parameters)
    {
      int retriesLeft = retryCount ?? NumOfRetries;
      if (retriesLeft == 0)
      {
        return await RequestAsync<T>(token, method, parameters);
      }
      do
      {
        try
        {
          retriesLeft--;
          RpcResponse<T> rpcResponse;
          if (method == "getblock" && (int)parameters[1] == 0)
          {
            object response = await MakeRequestReturnStreamAsync(token, new RpcRequest(1, method, parameters));
            return (T)response;
          }
          else
          {
            rpcResponse = await MakeRequestAsync<T>(token, new RpcRequest(1, method, parameters));
          }
          return rpcResponse.Result;
        }
        catch (TaskCanceledException)
        {
          throw;
        }
        catch (RpcException)
        {
          // Rethrow RPC exception, since this is probably a permanent error 
          throw;
        }
        catch (Exception ex)
        {
          if (retriesLeft == 0)
          {
            throw new Exception($"Failed after {NumOfRetries} retries. Last error: {ex.Message}",ex);
          }
          logger.LogError($"Error while execution RPC method {method} toward node {Address}. Retries left {retriesLeft}. Error:  {ex.Message}");
        }
        if (token.HasValue)
        {
          await Task.Delay(waitBetweenRetriesMs, token.Value);
        }
        else
        {
          await Task.Delay(waitBetweenRetriesMs);
        }

      } while (retriesLeft > 0);

      // Should not happen since we exit when retriesLeft == 0
      throw new Exception("Internal error RequestAsyncWithRetry  reached the end");

    }
  
    private async Task<RpcResponse<T>> MakeRequestAsync<T>(CancellationToken? token, RpcRequest rpcRequest)
    {
      using var httpResponse = await MakeHttpRequestAsync(token, rpcRequest);
      return await GetRpcResponseAsync<T>(httpResponse);
    }

    private async Task<RpcBitcoinStreamReader> MakeRequestReturnStreamAsync(CancellationToken? token, RpcRequest rpcRequest)
    {
      var httpResponse = await MakeHttpRequestAsync(token, rpcRequest);
      return await GetRpcResponseAsStreamAsync(httpResponse, token);
    }

    private HttpRequestMessage CreateRequestMessage(string json)
    {
      var reqMessage = new HttpRequestMessage(HttpMethod.Post, Address.AbsoluteUri);

      var byteArray = Encoding.ASCII.GetBytes($"{Credentials.UserName}:{Credentials.Password}");
      reqMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
      reqMessage.Content = new StringContent(json, new UTF8Encoding(false), "application/json-rpc");
      return reqMessage;
    }

    private async Task<HttpResponseMessage> MakeHttpRequestAsync(CancellationToken? token, RpcRequest rpcRequest)
    {
      string paramDescription = rpcRequest.Parameters?.FirstOrDefault()?.ToString() ?? "";
      if (rpcRequest.Parameters?.Count > 1)
      {
        paramDescription += ",...";
      }
      logger.LogInformation($"Calling method '{rpcRequest.Method}({paramDescription}) on node {Address.Host}:{Address.Port}");
      var reqMessage = CreateRequestMessage(rpcRequest.GetJSON());
      using var cts = new CancellationTokenSource(RequestTimeout);
      using var cts2=  CancellationTokenSource.CreateLinkedTokenSource(cts.Token, token ?? CancellationToken.None);
      
      var httpResponse = await HttpClient.SendAsync(reqMessage, cts2.Token).ConfigureAwait(false);
      if (!httpResponse.IsSuccessStatusCode)
      {
        var response = await GetRpcResponseAsync<string>(httpResponse);
        throw new RpcException(response.Error.code, response.Error.message, Address.AbsoluteUri);
      }
      return httpResponse;
    }

    private async Task<RpcResponse<T>> GetRpcResponseAsync<T>(HttpResponseMessage responseMessage)
    {
      string json = await responseMessage.Content.ReadAsStringAsync();

      if (string.IsNullOrEmpty(json))
      {
        return new RpcResponse<T>
        {
          Error = new RpcError
          {
            code = (int)responseMessage.StatusCode,
            message = responseMessage.ReasonPhrase
          }
        };
      }
      try
      {
        return JsonSerializer.Deserialize<RpcResponse<T>>(json);
      }
      catch (JsonException ex)
      {
        // Unable to parse error, so we return status code.
        throw new RpcException($"Error when executing bitcoin RPC method Response code {responseMessage.StatusCode} was returned, exception message was '{ex.Message}'.", Address.AbsoluteUri, ex);
      }
    }

    static readonly char[] validChars = new char[] { ':', '"', ' ', '\n', '\r', '\t' };

    private async Task ReadUntilAsync(char characterToFind, StreamReader streamReader, CancellationToken? token)
    {
      char[] character = new char[1];
      do
      {
        token?.ThrowIfCancellationRequested();

        await streamReader.ReadBlockAsync(character, 0, 1);
        if (!validChars.Contains(character[0]))
        {
          throw new RpcException($"Error when executing bitcoin RPC method. RPC response contains invalid JSON.", Address.AbsoluteUri, null);
        }
      } while (!streamReader.EndOfStream && character[0] != characterToFind);
    }

    /// <summary>
    /// This method bypasses JSON wrapper so it can stream the value part of the "result" field in JSON response which can contain huge amount of HEX encoded data, that JSON 
    /// parsers are unable to deserialize, and pass it out as a Stream so that NBitcoin can directly use the stream when creating instances
    /// </summary>
    private async Task<RpcBitcoinStreamReader> GetRpcResponseAsStreamAsync(HttpResponseMessage responseMessage, CancellationToken? token)
    {
      var responseStream = await responseMessage.Content.ReadAsStreamAsync();
      var strReader = new StreamReader(responseStream);
      // Bucket to hold data that is present between quotation marks, used to find field names
      StringBuilder bucket = new StringBuilder();
      do
      {
        token?.ThrowIfCancellationRequested();

        char[] charFromStream = new char[1];
        await strReader.ReadBlockAsync(charFromStream, 0, 1);
        
        // Once we find " we clear the content of the bucket to start storing a new value
        if (charFromStream[0] == '"')
        {
          // We found field name "result" now we just check if it looks like this "result":" before we return the stream with the position
          // on the first char after the "
          if (bucket.ToString().ToLower() == "result")
          {
            await ReadUntilAsync(':', strReader, token);
            await ReadUntilAsync('\"', strReader, token);

            return new RpcBitcoinStreamReader(strReader, token);
          }
          bucket.Clear();
        }
        else
        {
          bucket.Append(charFromStream);
        }

      } while (!strReader.EndOfStream);

      throw new RpcException($"Error when executing bitcoin RPC method. RPC response contains invalid JSON.", Address.AbsoluteUri, null);
    }

    public override string ToString()
    {
      return Address?.ToString();
    }
  }
}
