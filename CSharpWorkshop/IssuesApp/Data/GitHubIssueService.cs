using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace IssuesApp.Data
{
  public class GitHubIssueService : IIssueService
  {
    // Follow these steps to create a GitHub Access Token
    // https://help.github.com/articles/creating-a-personal-access-token-for-the-command-line/#creating-a-token
    // Select the following permissions for your GitHub Access Token:
    // - repo:status
    // - public_repo
    private static readonly string GitHubAccessToken = DecodeToken("NTBjZDc3OTRhYzAzYTRmMjYyYzQ2NzUwNmZlNWM0OGU2ODRlNjMwOA==");
    private static string DecodeToken(string base64EncodedToken) => Encoding.UTF8.GetString(Convert.FromBase64String(base64EncodedToken));

    private readonly HttpClient myHttpClient;

    public GitHubIssueService(HttpClient httpClient)
    {
      myHttpClient = httpClient;
      myHttpClient.DefaultRequestHeaders.Add("User-Agent", "GiHubQuery App");
    }

    public async Task<IEnumerable<Issue>> GetIssuesAsync(
      string ownerName,
      string repoName,
      IProgress<int> progress,
      CancellationToken cancellationToken)
    {
      const int maxIssuesCount = 200;
      var issues = new List<Issue>();

      var request = CreateRequest(ownerName, repoName);

      while (true)
      {
        await Task.Delay(1000, cancellationToken).ConfigureAwait(false);

        var responseBody = await PostAsync(request, cancellationToken).ConfigureAwait(false);
        var results = JObject.Parse(responseBody);

        var issuesResults = (JObject) results["data"]["repository"]["issues"];
        foreach (var issueObject in (JArray) issuesResults["nodes"])
        {
          var issue = CreateFromJson((JObject) issueObject);
          issues.Add(issue);
        }

        progress?.Report(issues.Count);

        var issuesPageInfo = (JObject) issuesResults["pageInfo"];
        var hasMorePages = (bool) issuesPageInfo["hasPreviousPage"];

        if (!hasMorePages || issues.Count >= maxIssuesCount)
        {
          break;
        }

        request.Variables["start_cursor"] = issuesPageInfo["startCursor"].ToString();

        cancellationToken.ThrowIfCancellationRequested();
      }

      return issues;
    }

    private static Issue CreateFromJson(JObject issueObject)
    {
      return new Issue
      {
        Title = (string) issueObject["title"],
        Url = new Uri((string) issueObject["url"]),
        CreatedAt = (DateTimeOffset) issueObject["createdAt"],
        State = ConvertToIssueState((string) issueObject["state"]),
        //Author = new Author
        //{
        //  Login = (string) issueObject["author"]["login"],
        //  Url = new Uri((string) issueObject["author"]["url"])
        //},
        Labels = ExtractLabels((JArray) issueObject["labels"]["nodes"])
      };

      IssueState ConvertToIssueState(string state)
      {
        switch (state)
        {
          case "CLOSED":
            return IssueState.Closed;
          case "OPEN":
            return IssueState.Open;
          default:
            return IssueState.Unknown;
        }
      }

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
      [JsonProperty("query")] public string Query { get; set; }

      [JsonProperty("variables")] public Dictionary<string, object> Variables { get; } = new Dictionary<string, object>();

      public string ToJsonText() => JsonConvert.SerializeObject(this);
    }

    private static GraphQLRequest CreateRequest(string ownerName, string repoName)
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

      request.Variables["owner_name"] = ownerName;
      request.Variables["repo_name"] = repoName;

      return request;
    }
  }
}