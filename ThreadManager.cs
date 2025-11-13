using System;
using System.Collections.Generic;
using MelonLoader;

namespace CMS21Together;

public class ThreadManager
{
	private static readonly List<Action> executeOnMainThread = new();
	private static readonly List<Action> executeCopiedOnMainThread = new();
	private static bool actionToExecuteOnMainThread;

	public static void UpdateThread()
	{
		UpdateMain();
	}

    public static void ExecuteOnMainThread<T>(Action<T> _action, T exception)
	{
		if (_action == null)
		{
			MelonLogger.Msg("No action to execute on main thread!");
			return;
		}

		lock (executeOnMainThread)
		{
			executeOnMainThread.Add(() =>
			{
				try
				{
					_action(exception);
				}
				catch (Exception e)
				{
					MelonLogger.Msg("Encoutered exception on MainThread: " + e);
				}
			});
			actionToExecuteOnMainThread = true;
		}
	}

	public static void UpdateMain()
	{
		if (actionToExecuteOnMainThread)
		{
			executeCopiedOnMainThread.Clear();
			lock (executeOnMainThread)
			{
				executeCopiedOnMainThread.AddRange(executeOnMainThread);
				executeOnMainThread.Clear();
				actionToExecuteOnMainThread = false;
			}

			for (var i = 0; i < executeCopiedOnMainThread.Count; i++) executeCopiedOnMainThread[i]();
		}
	}
}