using System.Threading;
using System.Threading.Tasks;

namespace VirtualCashcard
{
    public interface IPinValidateService
    {
        Task<bool> VerifyPin(long pin, CancellationToken cts);
    }
}
