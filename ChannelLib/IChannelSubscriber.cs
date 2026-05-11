namespace ChannelLib;

public interface IChannelSubscriber<T>
{
    T ChannelNameId { get; }
}
