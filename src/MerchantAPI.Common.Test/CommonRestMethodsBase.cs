// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.Common.Authentication;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace MerchantAPI.Common.Test
{
  public abstract class CommonRestMethodsBase<TGetViewModel, TPostViewModel, TAppSettings> : CommonTestRestBase<TAppSettings>
      where TGetViewModel : class
      where TPostViewModel : class
      where TAppSettings : CommonAppSettings, new()
  {
    public virtual string GetNonExistentKey() => "ThisKeyDoesNotExists";


    public abstract TPostViewModel GetItemToCreate();

    public abstract TPostViewModel[] GetItemsToCreate();

    /// <summary>
    /// Throws if entry "post" posted to server does not match the one returned from "get"
    /// </summary>
    public abstract void CheckWasCreatedFrom(TPostViewModel post, TGetViewModel get);

    // NOTE: we need separate ExtractXXXKeys methods because view and post model can be the same. 
    public abstract string ExtractGetKey(TGetViewModel entry);

    public abstract string ExtractPostKey(TPostViewModel entry);

    public abstract void SetPostKey(TPostViewModel entry, string key);

    public abstract void ModifyEntry(TPostViewModel entry);

    public virtual string UrlForKey(string key)
    {
      return GetBaseUrl() + "/" + HttpUtility.UrlEncode(key);
    }

    public string ChangeKeyCase(string s)
    {
      StringBuilder sb = new StringBuilder(s.Length);
      foreach (var c in s)
      {
        if (char.IsLower(c))
        {
          sb.Append(char.ToUpper(c));
        }
        else
        {
          // This will not convert non-letter characters
          sb.Append(char.ToLower(c));
        }
      }
      return sb.ToString();
    }


    [TestMethod]
    public async Task GetByID_NonExistingKey_ShouldReturn404()
    {
      var httpResponse = await PerformRequestAsync(client, HttpMethod.Get, UrlForKey(GetNonExistentKey()));
      Assert.AreEqual(HttpStatusCode.NotFound, httpResponse.StatusCode);
    }

    [TestMethod]
    public virtual async Task GetCollection_NoElements_ShouldReturn200Empty()
    {

      var httpResponse = await PerformRequestAsync(client, HttpMethod.Get, GetBaseUrl());
      Assert.AreEqual(HttpStatusCode.OK, httpResponse.StatusCode);
      var content = await httpResponse.Content.ReadAsStringAsync();
      Assert.AreEqual("[]", content);
    }

    [TestMethod]
    public async Task GetKeyShouldBeCaseInsensitive()
    {
      var item1 = GetItemToCreate();
      var item1Key = ExtractPostKey(item1);

      // Check that id does not exists (database is deleted at start of test)
      await Get<TGetViewModel>(client, UrlForKey(item1Key), HttpStatusCode.NotFound);

      // Create new one using POST
      await Post<TPostViewModel, TGetViewModel>(client, item1, HttpStatusCode.Created);

      // We should be able to retrieve it:
      var entry1Response = await Get<TGetViewModel>(client, UrlForKey(item1Key), HttpStatusCode.OK);
      Assert.AreEqual(item1Key, ExtractGetKey(entry1Response));

      // Retrieval by key should be case sensitive
      await Get<TGetViewModel>(client, UrlForKey(ChangeKeyCase(item1Key)), HttpStatusCode.OK);
    }

    [TestMethod]
    public async Task TestPost()
    {
      var entryPost = GetItemToCreate();
      var entryPostKey = ExtractPostKey(entryPost);

      // Check that id does not exists (database is deleted at start of test)
      await Get<TGetViewModel>(client, UrlForKey(ExtractPostKey(entryPost)), HttpStatusCode.NotFound);

      // Create new one using POST
      var (entryResponsePost, reponsePost) = await Post<TPostViewModel, TGetViewModel>(client, entryPost, HttpStatusCode.Created);

      CheckWasCreatedFrom(entryPost, entryResponsePost);


      // Case insensitive compare (HttpUtility encoded ':'. as %3a, where server encoded it as %3A).
      Assert.AreEqual(0, string.Compare(reponsePost.Headers.Location.AbsolutePath, UrlForKey(entryPostKey), StringComparison.OrdinalIgnoreCase));


      // And we should be able to retrieve the entry through GET
      var get2 = await Get<TGetViewModel>(client, UrlForKey(entryPostKey), HttpStatusCode.OK);

      // And entry returned by POST should be the same as entry returned by GET
      CheckWasCreatedFrom(entryPost, get2);
    }

    [TestMethod]
    public virtual async Task TestPost_2x_ShouldReturn409()
    {
      var entryPost = GetItemToCreate();

      var (entryResponsePost, reponsePost) = await Post<TPostViewModel, TGetViewModel>(client, entryPost, HttpStatusCode.Created);
      var (entryResponsePost2, reponsePost2) = await Post<TPostViewModel, TGetViewModel>(client, entryPost, HttpStatusCode.Conflict);
    }

    [TestMethod]
    public virtual async Task GetMultiple()
    {
      var entries = GetItemsToCreate();

      foreach (var entry in entries)
      {
        // Create new one using POST
        await Post<TPostViewModel, TGetViewModel>(client, entry, HttpStatusCode.Created);
      }

      // We should be able to retrieve it:
      var getEntries = await Get<TGetViewModel[]>(client,
        GetBaseUrl(), HttpStatusCode.OK);

      Assert.AreEqual(entries.Length, getEntries.Length);

      foreach (var postEntry in entries)
      {
        var postKey = ExtractPostKey(postEntry);
        var getEntry = getEntries.Single(x => ExtractGetKey(x) == postKey);
        CheckWasCreatedFrom(postEntry, getEntry);
      }
    }

    [TestMethod]
    public virtual async Task MultiplePost()
    {
      var entryPost = GetItemToCreate();

      var entryPostKey = ExtractPostKey(entryPost);

      // Check that id does not exists (database is deleted at start of test)
      await Get<TGetViewModel>(client, UrlForKey(entryPostKey), HttpStatusCode.NotFound);


      // Create new one using POST
      await Post<TPostViewModel, TGetViewModel>(client, entryPost, HttpStatusCode.Created);

      // Try to create it again - it should fail
      await Post<TPostViewModel, TGetViewModel>(client, entryPost, HttpStatusCode.Conflict);
    }

    [TestMethod]
    public virtual async Task Put()
    {
      var entryPost = GetItemToCreate();
      var entryPostKey = ExtractPostKey(entryPost);

      // Check that id does not exists (database is deleted at start of test)
      await Get<TGetViewModel>(client, UrlForKey(entryPostKey), HttpStatusCode.NotFound);


      // Try updating a non existent entry
      await Put(client, UrlForKey(entryPostKey), entryPost, HttpStatusCode.NotFound);

      // Create new one using POST
      await Post<TPostViewModel, TGetViewModel>(client, entryPost, HttpStatusCode.Created);

      // Update entry:
      ModifyEntry(entryPost);
      await Put(client, UrlForKey(entryPostKey), entryPost, HttpStatusCode.NoContent);

      var entryGot = await Get<TGetViewModel>(client, UrlForKey(entryPostKey), HttpStatusCode.OK);
      CheckWasCreatedFrom(entryPost, entryGot);


      // Try to modify entry by using primary key with different case
      entryPostKey = ChangeKeyCase(entryPostKey);
      SetPostKey(entryPost, entryPostKey);
      ModifyEntry(entryPost);
      await Put(client, UrlForKey(entryPostKey), entryPost, HttpStatusCode.NoContent);

      var entryGot2 = await Get<TGetViewModel>(client, UrlForKey(entryPostKey), HttpStatusCode.OK);
      CheckWasCreatedFrom(entryPost, entryGot2);
    }

    [TestMethod]
    public virtual async Task DeleteTest()
    {
      var entries = GetItemsToCreate();

      foreach (var entry in entries)
      {
        // Create new one using POST
        await Post<TPostViewModel, TGetViewModel>(client, entry, HttpStatusCode.Created);
      }

      // Check if all are there
      foreach (var entry in entries)
      {
        // Create new one using POST
        await Get<TGetViewModel>(client, UrlForKey(ExtractPostKey(entry)), HttpStatusCode.OK);
      }

      var firstKey = ExtractPostKey(entries.First());

      // Delete first one
      await Delete(client, UrlForKey(firstKey));

      // GET should not find the first anymore, but it should find the rest
      foreach (var entry in entries)
      {
        var key = ExtractPostKey(entry);
        await Get<TGetViewModel>(client, UrlForKey(key),

          key == firstKey ? HttpStatusCode.NotFound : HttpStatusCode.OK);
      }
    }

    [TestMethod]
    public virtual async Task Delete_NoElement_ShouldReturnNoContent()
    {
      // Delete always return NoContent to make (response) idempotent
      await Delete(client, UrlForKey(GetNonExistentKey()));
    }
  }
}
