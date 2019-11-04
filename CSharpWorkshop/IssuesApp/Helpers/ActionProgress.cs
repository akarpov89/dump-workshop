using System;

namespace IssuesApp.Helpers
{
  public class ActionProgress : IProgress<int>
  {
    private readonly Action<int> myOnProgress;

    public ActionProgress(Action<int> onProgress)
    {
      myOnProgress = onProgress;
    }

    public void Report(int value)
    {
      myOnProgress(value);
    }
  }
}