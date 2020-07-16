// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.APIGateway.Domain.Models;
using MerchantAPI.APIGateway.Domain.Repositories;
using MerchantAPI.APIGateway.Domain.ViewModels;
using MerchantAPI.Common;
using MerchantAPI.Common.Json;
using Microsoft.Extensions.Logging;
using NBitcoin;
using System;
using System.Linq;
using System.Threading.Tasks;
using MerchantAPI.APIGateway.Domain.ExternalServices;
using MerchantAPI.APIGateway.Domain.Models.Events;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace MerchantAPI.APIGateway.Domain.Actions
{
  public class NotificationAction : INotificationAction
  {
    static readonly ConcurrentDictionary<string, NotificationData> newNotificationEvents = new ConcurrentDictionary<string, NotificationData>();
    readonly ILogger<NotificationAction> logger;
    readonly ITxRepository txRepository;
    readonly IMinerId minerId;
    readonly IRpcMultiClient rpcMultiClient;
    IRestClient restClient;
    string last404ErrorCallbackUrl;

    public NotificationAction(ILogger<NotificationAction> logger, IMinerId minerId, IRpcMultiClient rpcMultiClient, ITxRepository txRepository)
    {
      this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
      this.rpcMultiClient = rpcMultiClient ?? throw new ArgumentNullException(nameof(rpcMultiClient));
      this.txRepository = txRepository ?? throw new ArgumentNullException(nameof(txRepository));
      this.minerId = minerId ?? throw new ArgumentNullException(nameof(minerId));

    }

    private NotificationData[] RemoveWaitingEvents(IEnumerable<NotificationData> notificationsList)
    {
      return notificationsList.Where(x => !newNotificationEvents.Any(y => GetTxIdFromKey(y.Key) == new uint256(x.TxExternalId))).ToArray();
    }

    private uint256 GetTxIdFromKey(string key)
    {
      return new uint256(key.Split(":")[1]);
    }

    private static string GetDictionaryKey(NewNotificationEvent e)
    {
      return $"{e.NotificationType}:{new uint256(e.TransactionId)}";
    }

    public static bool AddNotificationData(NewNotificationEvent e, NotificationData data)
    {
      return newNotificationEvents.TryAdd(GetDictionaryKey(e), data);
    }

    private IRestClient CreateRestClient(string url, string token)
    {
      if (restClient == null || restClient.BaseURL != url || restClient.Authorization != token)
      {
        restClient = new RestClient(url, token);
      }
      return restClient;
    }

    private async Task<bool> InitiateCallbackAsync(string callbackUrl, string callbackToken, string callBackEncryption, string payload)
    {
      // If we failed once because callback url is not reachable we will probably fail again, so we'll try again in next round of notification processing
      if (last404ErrorCallbackUrl == callbackUrl)
      {
        return false;
      }

      try
      {
        var client = CreateRestClient(callbackUrl, callbackToken);
        if (string.IsNullOrEmpty(callBackEncryption))
        {
          await client.PostJsonAsync("", payload);
        }
        else
        {
          await client.PostOctetStream("", MapiEncryption.Encrypt(payload, callBackEncryption));
        }

        return true;
      }
      catch (NotFoundException ex)
      {
        logger.LogError($"Callback failed. Error: {ex.GetBaseException().Message}");
        last404ErrorCallbackUrl = callbackUrl;
      }
      catch (Exception ex)
      {
        logger.LogError($"Callback failed. Error: {ex.GetBaseException().Message}");
      }
      return false;
    }

    string lastMinerId;
    async Task<string>  SignIfRequiredAsync<T>(T response)
    {
      string payload = HelperTools.JSONSerializeNewtonsoft(response, false);

      if (minerId == null)
      {
        // Do not sign if we do not have miner id
        return payload;
      }

      lastMinerId ??= await minerId.GetCurrentMinerIdAsync();

      async Task<JsonEnvelope> TryToSign()
      {
        Func<string, Task<(string signature, string publicKey)>> signWithMinerId = async sigHashHex =>
        {
          var signature = await minerId.SignWithMinerIdAsync(lastMinerId, sigHashHex);

          return (signature, lastMinerId);
        };

        var envelope = await JsonEnvelopeSignature.CreateJSonSignatureAsync(payload, signWithMinerId);

        // Verify a signature - some implementation might have incorrect race conditions when rotating the keys
        if (!JsonEnvelopeSignature.VerifySignature(envelope))
        {
          return null;
        }

        return envelope;
      }

      var jsonEnvelope = await TryToSign();

      if (jsonEnvelope == null)
      {
        throw new Exception("Error while validating signature. Possible reason: incorrect configuration or key rotation");
      }
      return HelperTools.JSONSerializeNewtonsoft(new SignedPayloadViewModel(jsonEnvelope), true);

    }

    private async Task<bool> SendNotificationAsync(string callbackReason, byte[] txIdBytes, byte[] blockHashBytes, long blockHeight, object payload, string callbackUrl, string callbackToken, string callbackEncryption)
    {
      var txId = new uint256(txIdBytes).ToString();
      var blockHash = (blockHashBytes == null || blockHashBytes.Length == 0) ? "" : new uint256(blockHashBytes).ToString();
      var cbNotification = new CallbackNotificationViewModel
      {
        APIVersion = Const.MERCHANT_API_VERSION,
        BlockHash = blockHash,
        BlockHeight = blockHeight,
        CallbackPayload = payload,
        CallbackReason = callbackReason,
        CallbackTxId = txId,
        MinerId = lastMinerId ?? await minerId.GetCurrentMinerIdAsync(),
        TimeStamp = DateTime.UtcNow
      };

      var signedPayload = await SignIfRequiredAsync(cbNotification);

      return await InitiateCallbackAsync(callbackUrl, callbackToken, callbackEncryption, signedPayload);

    }

    private async Task SendMerkleProofNotificationsAsync()
    {
      long rowsFetched = 0;

      do
      {
        // Fetch pending Merkle proof notifications and send them in batches of 10000 transactions
        var merkleProofTxs = (await txRepository.GetTxsToSendMerkleProofNotificationsAsync(rowsFetched, 10000)).ToArray();
        merkleProofTxs = RemoveWaitingEvents(merkleProofTxs);
        rowsFetched = merkleProofTxs.Count();
        foreach (var tx in merkleProofTxs)
        {
          var merkleProof = await rpcMultiClient.GetMerkleProofAsync(new uint256(tx.TxExternalId).ToString(), new uint256(tx.BlockHash).ToString());
          var success = await SendNotificationAsync(CallbackReason.MerkleProof, tx.TxExternalId, tx.BlockHash, tx.BlockHeight, merkleProof, tx.CallbackUrl, tx.CallbackToken, tx.CallbackEncryption);
          if (success)
          {
            await txRepository.SetMerkleProofSendDateAsync(tx.TxInternalId, tx.BlockInternalId, DateTime.UtcNow);
          }
        }
      } while (rowsFetched > 0);
    }

    private async Task SendBlockDoubleSpendNotificationsAsync()
    {
      // Fetch pending double spend block notifications and send them
      var doubleSpends = await txRepository.GetTxsToSendBlockDSNotificationsAsync();
      doubleSpends = RemoveWaitingEvents(doubleSpends);
      foreach (var tx in doubleSpends)
      {
        var success = await SendNotificationAsync(CallbackReason.DoubleSpend, tx.TxExternalId, tx.BlockHash, tx.BlockHeight, new { DoubleSpendTxId = new uint256(tx.DoubleSpendTxId).ToString(), tx.Payload }, tx.CallbackUrl, tx.CallbackToken, tx.CallbackEncryption);
        if (success)
        {
          await txRepository.SetBlockDoubleSpendSendDateAsync(tx.TxInternalId, tx.BlockInternalId, tx.DoubleSpendTxId, DateTime.UtcNow);
        }
      }
    }

    private async Task SendMempoolDoubleSpendNotificationsAsync()
    {
      // Fetch pending double spend mempool notifications and send them
      var doubleSpends = await txRepository.GetTxsToSendMempoolDSNotificationsAsync();
      doubleSpends = RemoveWaitingEvents(doubleSpends);
      foreach (var tx in doubleSpends)
      {
        var success = await SendNotificationAsync(CallbackReason.DoubleSpendAttempt, tx.TxExternalId, null, 0, new { DoubleSpendTxId = new uint256(tx.DoubleSpendTxId).ToString(), tx.Payload }, tx.CallbackUrl, tx.CallbackToken, tx.CallbackEncryption);
        if (success)
        {
          await txRepository.SetMempoolDoubleSpendSendDateAsync(tx.TxInternalId, tx.DoubleSpendTxId, DateTime.UtcNow);
        }
      }
    }

    public void ProcessAndSendNotifications()
    {
      try
      {
        var merkleTask = SendMerkleProofNotificationsAsync();
        var blockDSTask = SendBlockDoubleSpendNotificationsAsync();
        var mempoolDSTask = SendMempoolDoubleSpendNotificationsAsync();

        Task.WaitAll(new Task[] { merkleTask, blockDSTask, mempoolDSTask });
      }
      catch (Exception ex)
      {
        logger.LogError($"Error while processing notifications. Error :{ex.GetBaseException().Message}");
      }
    }

    /// <summary>
    /// Method for instant processing of notification events. Events that will fail here will be picked up by a background job for retrial.
    /// </summary>
    public async Task SendNotificationFromEventAsync(NewNotificationEvent e)
    {
      NotificationData tx;
      bool success;
      switch(e.NotificationType)
      {
        case CallbackReason.DoubleSpend:
          tx = await txRepository.GetTxToSendBlockDSNotificationAsync(e.TransactionId);
          success = await SendNotificationAsync(CallbackReason.DoubleSpend, tx.TxExternalId, tx.BlockHash, tx.BlockHeight, new { DoubleSpendTxId = new uint256(tx.DoubleSpendTxId).ToString(), tx.Payload }, tx.CallbackUrl, tx.CallbackToken, tx.CallbackEncryption);
          if (success)
          {
            await txRepository.SetBlockDoubleSpendSendDateAsync(tx.TxInternalId, tx.BlockInternalId, tx.DoubleSpendTxId, DateTime.UtcNow);
          }
          newNotificationEvents.TryRemove(GetDictionaryKey(e), out _);
          break;
        case CallbackReason.DoubleSpendAttempt:
          // we saved all necessary data to the dictionary so we will try to retrieve it here
          if (newNotificationEvents.TryGetValue(GetDictionaryKey(e), out tx))
          {
            success = await SendNotificationAsync(CallbackReason.DoubleSpendAttempt, tx.TxExternalId, null, 0, new { DoubleSpendTxId = new uint256(tx.DoubleSpendTxId).ToString(), tx.Payload }, tx.CallbackUrl, tx.CallbackToken, tx.CallbackEncryption);
            if (success)
            {
              await txRepository.SetMempoolDoubleSpendSendDateAsync(tx.TxInternalId, tx.DoubleSpendTxId, DateTime.UtcNow);
            }
          }
          newNotificationEvents.TryRemove(GetDictionaryKey(e), out _);
          break;
        case CallbackReason.MerkleProof:
          tx = await txRepository.GetTxToSendMerkleProofNotificationAsync(e.TransactionId);
          var merkleProof = await rpcMultiClient.GetMerkleProofAsync(new uint256(tx.TxExternalId).ToString(), new uint256(tx.BlockHash).ToString());
          success = await SendNotificationAsync(CallbackReason.MerkleProof, tx.TxExternalId, tx.BlockHash, tx.BlockHeight, merkleProof, tx.CallbackUrl, tx.CallbackToken, tx.CallbackEncryption);
          if (success)
          {
            await txRepository.SetMerkleProofSendDateAsync(tx.TxInternalId, tx.BlockInternalId, DateTime.UtcNow);
          }
          newNotificationEvents.TryRemove(GetDictionaryKey(e), out _);
          break;
      }
    }
  }
}
