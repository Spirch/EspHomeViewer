using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EspHomeLib.Interface;
public interface IProcessEventSubscriber
{
    Task<bool> GcCollected(bool alreadyCollected);
}
