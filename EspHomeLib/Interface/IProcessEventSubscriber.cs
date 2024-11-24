using System.Threading.Tasks;

namespace EspHomeLib.Interface;
public interface IProcessEventSubscriber
{
    Task<bool> GcCollected(bool alreadyCollected); //to do remove?
}
