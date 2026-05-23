using ExplorersIcebox.Enums;
using ExplorersIcebox.Util;
using ExplorersIcebox.Util.PathCreation;
namespace ExplorersIcebox.Scheduler.Tasks;

internal static class Task_GatherLoop
{
    public static void Enqueue()
    {
        Task_GatherMode.Enqueue();
        foreach(RouteClass.InteractionUtil wpList in IslandHelper.CurrentRoute.Value.BaseToLocation)
        {
            Task_BaseToGather.Enqueue(wpList.Waypoints, wpList.Mount, wpList.Fly);
        }

        int totalLoops = IslandHelper.GoalLoopAmount;
        if (C.RunMaxLoops)
            totalLoops = IslandHelper.MaxRouteLoops;
        for(int i = 0; i < totalLoops; i++)
        {
            foreach(RouteClass.InteractionUtil entry in IslandHelper.CurrentRoute.Value.RouteWaypoints)
            {
                Task_IslandInteract.Enqueue(entry.Waypoints, entry.TargetId, entry.Mount, entry.Fly);
            }
        }
        P.taskManager.Enqueue(() => CheckLoopCount(), "Checking loop count");
    }

    internal static bool? LoopCountUpdate(int currentLoops)
    {
        Svc.Log.Debug($"Maximum loop count: {IslandHelper.GoalLoopAmount}");
        Svc.Log.Debug($"Minimum Possible Loops: {IslandHelper.MaxRouteLoops}");
        Svc.Log.Debug($"Current loop count: {currentLoops}");
        int totalLoops = Math.Min(IslandHelper.GoalLoopAmount, IslandHelper.MaxRouteLoops) - currentLoops;
        Svc.Log.Debug($"Total loops expected: {totalLoops}");

        return true;
    }

    internal static bool? CheckLoopCount()
    {
        Svc.Log.Debug($"Current loop count: {IslandHelper.LoopCounter}");
        Svc.Log.Debug($"Max total loops: {IslandHelper.GoalLoopAmount}");
        int RepeatAmount = C.RunAmount;
        IslandHelper.LoopCounter += 1;
        if (C.RunMultiple && IslandHelper.LoopCounter < RepeatAmount)
        {
            Svc.Log.Debug($"Run multiple loops were enabled. \n" +
                          $"Current Loop: {IslandHelper.LoopCounter} \n" +
                          $"Repeat Amount: {RepeatAmount}");
            SchedulerMain.State = IceBoxState.Start;
        }
        else
        {
            SchedulerMain.State = IceBoxState.EndProcess;
        }

        return true;
    }
}
