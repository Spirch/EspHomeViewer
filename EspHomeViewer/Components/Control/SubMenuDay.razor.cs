using BlazorContextMenu;
using Microsoft.AspNetCore.Components;
using SseLib.Core.Option;

namespace EspHomeViewer.Components.Control;

public partial class SubMenuDay
{
    [EditorRequired, Parameter]
    public EventCallback<ItemClickEventArgs> OnMenuGraphClickAsync { get; set; }


    [EditorRequired, Parameter]
    public StatusInfoOption StatusInfo { get; set; }
}
