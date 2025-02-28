using System;
using System.Linq;
using GlazeWM.Domain.Common.Utils;
using GlazeWM.Domain.Containers;
using GlazeWM.Domain.Containers.Commands;
using GlazeWM.Domain.Workspaces;
using GlazeWM.Infrastructure.Bussing;
using GlazeWM.Infrastructure.WindowsApi.Events;
using Microsoft.Extensions.Logging;

namespace GlazeWM.Domain.Windows.EventHandlers
{
  internal class WindowMinimizeEndedHandler : IEventHandler<WindowMinimizeEndedEvent>
  {
    private readonly Bus _bus;
    private readonly WindowService _windowService;
    private readonly ContainerService _containerService;
    private readonly ILogger<WindowMinimizeEndedHandler> _logger;

    public WindowMinimizeEndedHandler(
      Bus bus,
      WindowService windowService,
      ContainerService containerService,
      ILogger<WindowMinimizeEndedHandler> logger
    )
    {
      _bus = bus;
      _windowService = windowService;
      _containerService = containerService;
      _logger = logger;
    }

    public void Handle(WindowMinimizeEndedEvent @event)
    {
      var window = _windowService.GetWindows()
        .FirstOrDefault(window => window.Hwnd == @event.WindowHandle) as MinimizedWindow;

      if (window == null)
        return;

      _logger.LogWindowEvent("Window minimize ended", window);

      var restoredWindow = CreateWindowFromPreviousState(window);

      _bus.Invoke(new ReplaceContainerCommand(restoredWindow, window.Parent, window.Index));

      if (restoredWindow is not TilingWindow)
        return;

      var workspace = WorkspaceService.GetWorkspaceFromChildContainer(window);
      var insertionTarget = workspace.LastFocusedDescendantOfType(typeof(IResizable));

      // Insert the created tiling window after the last focused descendant of the workspace.
      if (insertionTarget == null)
        _bus.Invoke(new MoveContainerWithinTreeCommand(restoredWindow, workspace, 0, true));
      else
        _bus.Invoke(
          new MoveContainerWithinTreeCommand(
            restoredWindow,
            insertionTarget.Parent,
            insertionTarget.Index + 1,
            true
          )
        );

      _containerService.ContainersToRedraw.Add(workspace);
      _bus.Invoke(new RedrawContainersCommand());
    }

    private static Window CreateWindowFromPreviousState(MinimizedWindow window)
    {
      return window.PreviousState switch
      {
        WindowType.FLOATING => new FloatingWindow(
          window.Hwnd,
          window.FloatingPlacement,
          window.BorderDelta
        ),
        WindowType.MAXIMIZED => new MaximizedWindow(
          window.Hwnd,
          window.FloatingPlacement,
          window.BorderDelta
        ),
        WindowType.FULLSCREEN => new FullscreenWindow(
          window.Hwnd,
          window.FloatingPlacement,
          window.BorderDelta
        ),
        // Set `SizePercentage` to 0 to correctly resize the container when moved within tree.
        WindowType.TILING => new TilingWindow(
          window.Hwnd,
          window.FloatingPlacement,
          window.BorderDelta,
          0
        ),
        WindowType.MINIMIZED => throw new ArgumentException(null, nameof(window)),
        _ => throw new ArgumentException(null, nameof(window)),
      };
    }
  }
}
