using System;

namespace IssuesApp.Data
{
  public static class IssueExtensions
  {
    public static string Categorize(this Issue issue)
    {
      var subsystem = "";
      var languageFeature = "";

      if (issue.Labels != null)
      {
        foreach (var label in issue.Labels)
        {
          switch (Split2(label, "-"))
          {
            case ("AREA", var area):
              subsystem = area;
              break;
            case ("IDE", var ideSubsystem):
              subsystem = ideSubsystem;
              break;
            case ("NEW LANGUAGE FEATURE", var langFeature):
              languageFeature = langFeature;
              break;
          }
        }
      }

      return languageFeature != "" ? languageFeature : subsystem;
    }

    private static (string, string) Split2(string input, string separator)
    {
      var separatorIndex = input.IndexOf(separator, StringComparison.OrdinalIgnoreCase);
      if (separatorIndex > 0)
      {
        return (input.Substring(0, separatorIndex).Trim().ToUpper(),
                input.Substring(separatorIndex + 1).Trim());
      }

      return default;
    }
  }
}