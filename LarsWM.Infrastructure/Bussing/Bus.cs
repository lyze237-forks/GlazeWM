﻿using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reactive.Subjects;
using System.Windows;

namespace LarsWM.Infrastructure.Bussing
{
  /// <summary>
  /// Bus facilitates communication to command and event handlers.
  /// </summary>
  public sealed class Bus
  {
    public readonly Subject<Event> Events = new Subject<Event>();
    private static readonly Object _lockObj = new Object();

    /// <summary>
    /// Sends command to appropriate command handler.
    /// </summary>
    public CommandResponse Invoke<T>(T command) where T : Command
    {
      // Create a `Type` object representing the constructed `ICommandHandler` generic.
      var handlerType = typeof(ICommandHandler<>).MakeGenericType(command.GetType());

      Debug.WriteLine($"Command {command.Name} invoked.");

      try
      {
        var handlerInstance = ServiceLocator.Provider.GetRequiredService(handlerType) as ICommandHandler<T>;
        lock (_lockObj)
        {
          return handlerInstance.Handle(command);
        }
      }
      catch (Exception error)
      {
        // Alert the user of the error.
        // TODO: This throws duplicate errors if a command errors and it was invoked by another handler.
        if (error is FatalUserException)
          MessageBox.Show(error.Message);

        File.AppendAllText("./errors.log", $"\n\n{error.Message + error.StackTrace}");
        throw error;
      }
    }

    /// <summary>
    /// Sends event to appropriate event handlers.
    /// </summary>
    public void RaiseEvent<T>(T @event) where T : Event
    {
      // Create a `Type` object representing the constructed `IEventHandler` generic.
      var handlerType = typeof(IEventHandler<>).MakeGenericType(@event.GetType());

      Debug.WriteLine($"Event {@event.Name} emitted.");

      try
      {
        var handlerInstances = ServiceLocator.Provider.GetServices(handlerType) as IEnumerable<IEventHandler<T>>;

        foreach (var handler in handlerInstances)
        {
          lock (_lockObj)
          {
            handler.Handle(@event);
          }
        }

        // Emit event through subject.
        Events.OnNext(@event);
      }
      catch (Exception error)
      {
        // Alert the user of the error.
        if (error is FatalUserException)
          MessageBox.Show(error.Message);

        File.AppendAllText("./errors.log", $"\n\n{error.Message + error.StackTrace}");
        throw error;
      }
    }
  }
}
