using System;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

// 명령 실행 결과
public class CommandResult
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public int Damage { get; set; }
    public int Healing { get; set; }
    public List<string> Effects { get; set; } = new List<string>();

    public CommandResult(bool success, string message = "")
    {
        Success = success;
        Message = message;
    }
}

// 명령 컨텍스트 (실행에 필요한 정보)
public class CommandContext
{
    public BattleActor Actor { get; set; }
    public BattleActor Target { get; set; }
    public BattleCharInfoNew ActorInfo { get; set; }
    public BattleCharInfoNew TargetInfo { get; set; }
    public int SkillId { get; set; }
    public int ItemId { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();

    public CommandContext(BattleActor actor, BattleActor target = null)
    {
        Actor = actor;
        Target = target;
        ActorInfo = actor?.BattleActorInfo;
        TargetInfo = target?.BattleActorInfo;
    }
}

// 명령 인터페이스
public interface IBattleCommand
{
    string CommandName { get; }
    bool CanExecute(CommandContext context);
    UniTask<CommandResult> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default);
    UniTask UndoAsync(CommandContext context, CancellationToken cancellationToken = default);
}


// 명령 히스토리 (Undo/Redo를 위한)
public class CommandHistory
{
    private readonly Stack<(IBattleCommand command, CommandContext context, CommandResult result)> executedCommands;
    private readonly Stack<(IBattleCommand command, CommandContext context, CommandResult result)> undoneCommands;

    public CommandHistory()
    {
        executedCommands = new Stack<(IBattleCommand, CommandContext, CommandResult)>();
        undoneCommands = new Stack<(IBattleCommand, CommandContext, CommandResult)>();
    }

    public void AddCommand(IBattleCommand command, CommandContext context, CommandResult result)
    {
        executedCommands.Push((command, context, result));
        undoneCommands.Clear(); // 새 명령 실행시 Redo 스택 클리어
    }

    public async UniTask<bool> UndoLastCommand(CancellationToken cancellationToken = default)
    {
        if (executedCommands.Count == 0)
            return false;

        var (command, context, result) = executedCommands.Pop();
        await command.UndoAsync(context, cancellationToken);
        undoneCommands.Push((command, context, result));

        return true;
    }

    public async UniTask<bool> RedoLastCommand(CancellationToken cancellationToken = default)
    {
        if (undoneCommands.Count == 0)
            return false;

        var (command, context, result) = undoneCommands.Pop();
        var newResult = await command.ExecuteAsync(context, cancellationToken);
        executedCommands.Push((command, context, newResult));

        return true;
    }

    public void Clear()
    {
        executedCommands.Clear();
        undoneCommands.Clear();
    }

    public List<string> GetHistory()
    {
        var history = new List<string>();
        foreach (var (command, context, result) in executedCommands)
        {
            history.Add($"{command.CommandName}: {result.Message}");
        }
        return history;
    }
}