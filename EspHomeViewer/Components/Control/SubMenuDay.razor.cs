using BlazorContextMenu;
using EspHomeLib.Option;
using Microsoft.AspNetCore.Components;

namespace EspHomeViewer.Components.Control;

public partial class SubMenuDay
{
    [EditorRequired, Parameter]
    public EventCallback<ItemClickEventArgs> OnMenuGraphClickAsync { get; set; }


    [EditorRequired, Parameter]
    public StatusInfoOption StatusInfo { get; set; }
}
