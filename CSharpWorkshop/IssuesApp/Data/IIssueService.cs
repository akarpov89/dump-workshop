using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace IssuesApp.Data
{
  public interface IIssueService
  {
    IAsyncEnumerable<Issue> GetIssuesAsync(string ownerName, string repoName, CancellationToken cancellationToken = default);
  }

  public class Issue
  {
    public string? Title { get; set; }
    public Uri? Url { get; set; }
    public Author? Author { get; set; }
    public IssueState State { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string[]? Labels { get; set; }

    public bool TryGetAuthorLogin([NotNullWhen(true)] out string? login)
    {
      login = Author?.Login;
      return login != null;
    }

    public void Test()
    {
      if (TryGetAuthorLogin(out var login))
      {
        var loginLength = login.Length;
      }
    }

    public void NullifyAuthor()
    {
      Author = null;
    }

    public void Test2()
    {
      if (Author != null)
      {
        NullifyAuthor();

        var x = Author.Login.Length;
      }
    }
  }

  public class Author
  {
    public string? Login { get; set; }
    public Uri? Url { get; set; }
  }

  public enum IssueState
  {
    Unknown,
    Open,
    Closed
  }
}