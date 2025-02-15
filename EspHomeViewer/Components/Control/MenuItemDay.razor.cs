using BlazorContextMenu;
using Microsoft.AspNetCore.Components;

namespace EspHomeViewer.Components.Control;

public partial class MenuItemDay
{
    [EditorRequired, Parameter]
    public EventCallback<ItemClickEventArgs> OnMenuGraphClickAsync { get; set; }
}
