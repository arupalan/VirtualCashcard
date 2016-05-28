using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VirtualCashcard
{
    public class Cashcard
    {
        private decimal dBalance;
        private readonly IPinValidateService pinValidator;
        private CancellationTokenSource cts;
        private static readonly object Locker = new object();

        public Cashcard(IPinValidateService pinValidator)
        {
            this.pinValidator = pinValidator;
        }

        public decimal Balance
        {
            get
            {
                lock (Locker)
                {
                    return dBalance;
                }
            }
        }

        /// <summary>
        /// Valid pin and no timeout then top up balance with topup amount
        /// </summary>
        /// <param name="pin"></param>
        /// <param name="amount"></param>
        /// <param name="timeout"></param>
        /// <returns>true or false</returns>
        /// <Exception>OperationCanceledException on timeout</Exception>
        public async Task<bool> TopupbalanceAsync(long pin, decimal amount, TimeSpan timeout)
        {
            cts = new CancellationTokenSource();
            cts.CancelAfter(timeout);
            //First verify pin access to avoid hackers going beyond this code
            var access = await pinValidator.VerifyPin(pin, cts.Token);
            cts.Dispose();

            //If valid access then check balance to complete transaction
            if (access != true) return false;
            if (amount.Equals(decimal.MaxValue)) return false;
            //The internal representation of decimal is too complex for modifications 
            //to be made with atomic instructions at the CPU level, use lock.
            lock (Locker)
            {
                dBalance = decimal.Add(dBalance,amount);
                return true;
            }
        }

        /// <summary>
        /// Valid pin and adequate balance and no timeout
        /// then adjust balance with withdrawal amount 
        /// </summary>
        /// <param name="pin"> user identity pin</param>
        /// <param name="amount">amount to be withdrawn from balance</param>
        /// <param name="timeout">timeout for pin validation operation</param>
        /// <returns>true with balance adjusted else false</returns>
        /// <Exception>OperationCanceledException on timeout</Exception>
        public async Task<bool> WithdrawAsync(long pin,decimal amount,TimeSpan timeout)
        {
            cts = new CancellationTokenSource();
            cts.CancelAfter(timeout);
            //First verify pin access to avoid hackers going beyond this code
            var access = await pinValidator.VerifyPin(pin,cts.Token);
            cts.Dispose();

            //If valid access then check balance to complete transaction
            if (access != true) return false;
            //The internal representation of decimal is too complex for modifications 
            //to be made with atomic instructions at the CPU level, use lock.
            lock(Locker)
            {
                if (decimal.Subtract(dBalance,amount) <= 0.001M) return false;
                dBalance = decimal.Subtract(dBalance, amount);
                return true;
            }
        }
    }
}
