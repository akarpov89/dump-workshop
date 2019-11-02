﻿using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace IssuesApp.Data
{
  public class GitHubIssueService : IIssueService, IDisposable, IAsyncDisposable
  {
    // Follow these steps to create a GitHub Access Token
    // https://help.github.com/articles/creating-a-personal-access-token-for-the-command-line/#creating-a-token
    // Select the following permissions for your GitHub Access Token:
    // - repo:status
    // - public_repo
    private const string GitHubAccessToken = "478b248a7a9a405453c26b73f3e2f9031fec9eba";

    private readonly string myOwner;
    private readonly string myRepoName;
    private readonly HttpClient myHttpClient;

    public GitHubIssueService(string owner, string repoName)
    {
      myOwner = owner;
      myRepoName = repoName;

      myHttpClient = new HttpClient();
      myHttpClient.DefaultRequestHeaders.Add("User-Agent", "GiHubQuery App");
    }

    public async IAsyncEnumerable<Issue> GetIssuesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
      var request = CreateRequest();

      while (true)
      {
        await Task.Delay(1000, cancellationToken).ConfigureAwait(false);

        var responseBody = await PostAsync(request, cancellationToken).ConfigureAwait(false);
        var results = JObject.Parse(responseBody);

        var issuesResults = (JObject) results["data"]["repository"]["issues"];
        foreach (var issueObject in (JArray) issuesResults["nodes"])
        {
          var issue = CreateFromJson((JObject) issueObject);
          yield return issue;
        }

        var issuesPageInfo = (JObject) issuesResults["pageInfo"];
        var hasMorePages = (bool) issuesPageInfo["hasPreviousPage"];

        if (!hasMorePages)
        {
          yield break;
        }

        request.Variables["start_cursor"] = issuesPageInfo["startCursor"].ToString();

        cancellationToken.ThrowIfCancellationRequested();
      }
    }

    private static Issue CreateFromJson(JObject issueObject)
    {
      return new Issue
      {
        Title = (string) issueObject["title"],
        Url = new Uri((string) issueObject["url"]),
        CreatedAt = (DateTimeOffset) issueObject["createdAt"],
        State = ConvertToIssueState((string) issueObject["state"]),
        Author = new Author
        {
          Login = (string) issueObject["author"]["login"],
          Url = new Uri((string) issueObject["author"]["url"])
        },
        Labels = ExtractLabels((JArray) issueObject["labels"]["nodes"])
      };

      IssueState ConvertToIssueState(string state) => state switch
      {
        "CLOSED" => IssueState.Closed,
        "OPEN" => IssueState.Open,
        _ => IssueState.Unknown
      };

      string[] ExtractLabels(JArray labelsArray)
      {
        var result = new string[labelsArray.Count];
        for (var index = 0; index < labelsArray.Count; index++)
        {
          result[index] = (string) labelsArray[index]["name"];
        }

        return result;
      }
    }

    private async Task<string> PostAsync(GraphQLRequest request, CancellationToken token)
    {
      var postBody = request.ToJsonText();

      var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, new Uri("https://api.github.com/graphql"));

      httpRequestMessage.Headers.Add("Authorization", $"Token {GitHubAccessToken}");
      httpRequestMessage.Content = new StringContent(postBody, Encoding.UTF8, "application/json");

      var httpResponseMessage = await myHttpClient.SendAsync(httpRequestMessage, token).ConfigureAwait(false);

      httpResponseMessage.EnsureSuccessStatusCode();

      return await httpResponseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);
    }

    private class GraphQLRequest
    {
      [JsonProperty("query")] public string? Query { get; set; }

      [JsonProperty("variables")] public Dictionary<string, object> Variables { get; } = new Dictionary<string, object>();

      public string ToJsonText() => JsonConvert.SerializeObject(this);
    }

    private GraphQLRequest CreateRequest()
    {
      const string pagedIssueQuery =
        @"query ($owner_name: String!, $repo_name: String!,  $start_cursor:String) {
            repository(owner: $owner_name, name: $repo_name) {
            issues(last: 25, before: $start_cursor) {
              pageInfo {
                hasPreviousPage
                startCursor
              }
              nodes {
                title
                url
                number
                createdAt
                state
                author {
                  login
                  url
                }
                labels(first: 5) {
                  nodes {
                    name
                    description
                  }
                }
              }
            }
          }
        }";

      var request = new GraphQLRequest {Query = pagedIssueQuery};

      request.Variables["owner_name"] = myOwner;
      request.Variables["repo_name"] = myRepoName;

      return request;
    }

    public void Dispose()
    {
      Thread.Sleep(1000);
      myHttpClient.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
      await Task.Delay(1000);
      myHttpClient.Dispose();
    }
  }
}