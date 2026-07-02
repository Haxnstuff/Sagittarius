# Sagittarius

Sagittarius is a dual panel explorer application built to be small and aesthetically pleasing (to me). It has simple layout customisation/navigation.

## __Features__

* Dual Pane Layout: You can toggle split views using the keyboard command Ctrl S or the Command Window.
* Sidebar Customisation: Pinned folders can be reordered, unpinned, and moved up or down.
* Custom Highlight: Hovering over items applies a custom color highlight over the whole thing.
* Other normal file explorer stuff

## Sidebar Instructions

### Pinning Folders
* Navigate to the folder you want to pin.
* Right click the folder in the file list.
* Select Pin to Sidebar from the context menu.

### Unpinning Folders
* Right click the pinned folder in the sidebar list.
* Select Unpin from the context menu.

### Reordering Pinned Folders
* Method 1: Left click and drag a pinned sidebar item, then drop it on another pinned item to reorder.
* Method 2: Right click a pinned folder in the sidebar and select Move Up or Move Down. Pinned folders at the very top will not show Move Up, and pinned folders at the very bottom will not show Move Down.

## Keybindings
* J/K (Or arrows): Move selection down/up.
* H/L: Go back in history/Open folder.
* U: Go up one directory level.
* Tab: Toggle focus between active panes.
* Ctrl S: Toggle split view.
* Ctrl T: Open new tab.
* Ctrl W: Close active tab.
* Ctrl C: Copy selected item.
* Ctrl X: Cut selected item.
* Ctrl V: Paste item.
* F2: Rename selected item.
* Delete: Delete selected item.
* Ctrl Shift P: Open Command Window.

## Compilation
You can compile the application using the compilation script:
* Run build.bat in the command shell.
