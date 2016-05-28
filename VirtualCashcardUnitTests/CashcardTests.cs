using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using VirtualCashcard;

namespace VirtualCashcardUnitTests
{
    [TestFixture]
    public class CashcardTests
    {
        [Test]
        public async Task CanTopupArbitraryAmount()
        {
            Mock<IPinValidateService> mockPinValidator = new Mock<IPinValidateService>();
            var timeOut = new TimeSpan(0, 0, 0, 0, 100);
            mockPinValidator.Setup(f => f.VerifyPin(It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
                .Returns<long, CancellationToken>((x, y) => Task.FromResult(true));

            var cashCard = new VirtualCashcard.Cashcard(mockPinValidator.Object);
            var prebalance = cashCard.Balance;
            var result = await cashCard.TopupbalanceAsync(500, 200M, timeOut);
            Assert.That(result, Is.True);
            Assert.AreEqual(decimal.Add(prebalance,200M),cashCard.Balance);
        }

        [Test]
        public async Task TopUpFailOnMaxDecimalAmount()
        {
            Mock<IPinValidateService> mockPinValidator = new Mock<IPinValidateService>();
            mockPinValidator.Setup(f => f.VerifyPin(It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
                .Returns<long, CancellationToken>( (x, y) => Task.FromResult(true));

            var cashCard = new VirtualCashcard.Cashcard(mockPinValidator.Object);
            var prebalance = cashCard.Balance;
            var result = await cashCard.TopupbalanceAsync(500, decimal.MaxValue, new TimeSpan(300));
            Assert.That(result, Is.False);
            Assert.AreEqual(prebalance, cashCard.Balance);
        }

        [Test]
        public async Task TopupFailOnFailedPinVerification()
        {
            Mock<IPinValidateService> mockPinValidator = new Mock<IPinValidateService>();
            mockPinValidator.Setup(f => f.VerifyPin(It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
                .Returns<long, CancellationToken>((x, y) => Task.FromResult(false));

            var cashCard = new VirtualCashcard.Cashcard(mockPinValidator.Object);
            var prebalance = cashCard.Balance;
            var result = await cashCard.TopupbalanceAsync(500, 200M, new TimeSpan(300));
            Assert.That(result, Is.False);
            Assert.AreEqual(prebalance, cashCard.Balance);
        }


        [Test]
        public async Task TopupThrowsExceptionOnTimeOut()
        {
            Mock<IPinValidateService> mockPinValidator = new Mock<IPinValidateService>();
            var timeOut = new TimeSpan(0,0,0,0,100);
            mockPinValidator.Setup(f => f.VerifyPin(It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
                .Returns<long, CancellationToken>(async (x, y) =>
                {
                    await Task.Delay(timeOut.Add(timeOut).Milliseconds, y);
                    return true;
                });
            var cashCard = new VirtualCashcard.Cashcard(mockPinValidator.Object);
            var prebalance = cashCard.Balance;
            Assert.That( async () => await cashCard.TopupbalanceAsync(500, 200M, timeOut),
                Throws.TypeOf<TaskCanceledException>());
            Assert.AreEqual(prebalance, cashCard.Balance);
        }

        [Test]
        public async Task CanTopupFromMultiplePlacesSameTime()
        {
            Mock<IPinValidateService> mockPinValidator = new Mock<IPinValidateService>();
            var timeOut = new TimeSpan(0, 0, 0, 0, 100);
            mockPinValidator.Setup(f => f.VerifyPin(It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
                .Returns<long, CancellationToken>(async (x, y) =>
                {
                    await Task.Delay(timeOut.Milliseconds, y).ConfigureAwait(false);
                    return true;
                });

            var cashCard = new VirtualCashcard.Cashcard(mockPinValidator.Object);
            var prebalance = cashCard.Balance;

            //Note 3 separate pins used to top up balance on same cash card. Is it a bug?  
            //SOLID SRP will suggest that user management is not a feature of cash card hence presummably not a bug.
            Task innerTask1 = Task.Factory.StartNew(async () => { await cashCard.TopupbalanceAsync(525, 200M, timeOut.Add(timeOut)); });
            Task innerTask2 = Task.Factory.StartNew(async () => { await cashCard.TopupbalanceAsync(625, 300M, timeOut.Add(timeOut)); });
            Task innerTask3 = Task.Factory.StartNew(async () => { await cashCard.TopupbalanceAsync(725, 400M, timeOut.Add(timeOut)); });

            var task = Task.Factory.ContinueWhenAll(
                new[] { innerTask1, innerTask2, innerTask3 },
                innerTasks =>
                {
                    foreach (var innerTask in innerTasks)
                    {
                        Assert.That(innerTask.IsFaulted,Is.False);
                    }
                    Assert.AreEqual(cashCard.Balance,decimal.Add(prebalance,900M));
                });
        }

        [Test]
        public async Task CanWithdrawAmountWhenSufficientBalance()
        {
            Mock<IPinValidateService> mockPinValidator = new Mock<IPinValidateService>();
            var timeOut = new TimeSpan(0, 0, 0, 0, 100);
            mockPinValidator.Setup(f => f.VerifyPin(It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
                .Returns<long, CancellationToken>((x, y) => Task.FromResult(true));

            var cashCard = new VirtualCashcard.Cashcard(mockPinValidator.Object);
            var prebalance = cashCard.Balance;
            var result = await cashCard.TopupbalanceAsync(500, 200M, timeOut.Add(timeOut));
            Assert.That(result, Is.True);
            Assert.AreEqual(decimal.Add(prebalance,200M),cashCard.Balance);
            prebalance = cashCard.Balance;
            result = await cashCard.WithdrawAsync(500, 100M, timeOut);
            Assert.That(result, Is.True);
            Assert.AreEqual(decimal.Subtract(prebalance, 100M), cashCard.Balance);
        }

        [Test]
        public async Task WithdrawFailOnPinVerificationFailure()
        {
            Mock<IPinValidateService> mockPinValidator = new Mock<IPinValidateService>();
            var timeOut = new TimeSpan(0, 0, 0, 0, 100);
            mockPinValidator.Setup(f => f.VerifyPin(It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
                .Returns<long, CancellationToken>((x, y) => Task.FromResult(true));

            var cashCard = new VirtualCashcard.Cashcard(mockPinValidator.Object);
            var prebalance = cashCard.Balance;
            var result = await cashCard.TopupbalanceAsync(500, 200M, timeOut.Add(timeOut));
            Assert.That(result, Is.True);
            Assert.AreEqual(decimal.Add(prebalance, 200M), cashCard.Balance);
            prebalance = cashCard.Balance;

            mockPinValidator.Setup(f => f.VerifyPin(It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
                .Returns<long, CancellationToken>((x, y) => Task.FromResult(false));
            result = await cashCard.WithdrawAsync(500, 100M, timeOut);
            Assert.That(result, Is.False);
            Assert.AreEqual(prebalance, cashCard.Balance);
        }

        [Test]
        public async Task WithdrawFailWhenInSufficientBalance()
        {
            Mock<IPinValidateService> mockPinValidator = new Mock<IPinValidateService>();
            var timeOut = new TimeSpan(0, 0, 0, 0, 100);
            mockPinValidator.Setup(f => f.VerifyPin(It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
                .Returns<long, CancellationToken>((x, y) => Task.FromResult(true));

            var cashCard = new VirtualCashcard.Cashcard(mockPinValidator.Object);
            var prebalance = cashCard.Balance;
            var result = await cashCard.TopupbalanceAsync(500, 200M, timeOut.Add(timeOut));
            Assert.That(result, Is.True);
            Assert.AreEqual(decimal.Add(prebalance, 200M), cashCard.Balance);
            prebalance = cashCard.Balance;
            result = await cashCard.WithdrawAsync(500, 400M, timeOut);
            Assert.That(result, Is.False);
            Assert.AreEqual(prebalance, cashCard.Balance);
        }

        [Test]
        public async Task WithdrawFailOnTimeOut()
        {
            Mock<IPinValidateService> mockPinValidator = new Mock<IPinValidateService>();
            var timeOut = new TimeSpan(0, 0, 0, 0, 100);
            mockPinValidator.Setup(f => f.VerifyPin(It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
                .Returns<long, CancellationToken>((x, y) => Task.FromResult(true));

            var cashCard = new VirtualCashcard.Cashcard(mockPinValidator.Object);
            var prebalance = cashCard.Balance;
            var result = await cashCard.TopupbalanceAsync(500, 200M, timeOut.Add(timeOut));
            Assert.That(result, Is.True);
            Assert.AreEqual(decimal.Add(prebalance, 200M), cashCard.Balance);

            mockPinValidator.Setup(f => f.VerifyPin(It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
                .Returns<long, CancellationToken>(async (x, y) =>
                {
                    await Task.Delay(3*timeOut.Milliseconds, y);
                    return true;
                });

            prebalance = cashCard.Balance;
            Assert.That(async () => await cashCard.WithdrawAsync(500, 200M, timeOut),
                Throws.TypeOf<TaskCanceledException>());
            Assert.AreEqual(prebalance, cashCard.Balance); ;

        }

        [Test]
        public async Task CanWithDrawFromMultiplePlacesSameTime()
        {
            Mock<IPinValidateService> mockPinValidator = new Mock<IPinValidateService>();
            var timeOut = new TimeSpan(0, 0, 0, 0, 100);
            mockPinValidator.Setup(f => f.VerifyPin(It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
                .Returns<long, CancellationToken>(async (x, y) =>
                {
                    await Task.Delay(timeOut.Milliseconds, y).ConfigureAwait(false);
                    return true;
                });

            var cashCard = new VirtualCashcard.Cashcard(mockPinValidator.Object);
            var prebalance = cashCard.Balance;
            await cashCard.TopupbalanceAsync(525, 900M, timeOut.Add(timeOut));
            Assert.AreEqual(cashCard.Balance, decimal.Add(prebalance, 900M));

            //Note 3 separate pins used to WithdrawAsyncp balance on same cash card. Is it a bug?  
            //SOLID SRP will suggest that user management is not a feature of cash card hence presummably not a bug.
            Task innerTask1 = Task.Factory.StartNew(async () => { await cashCard.WithdrawAsync(425, 100M, timeOut.Add(timeOut)); });
            Task innerTask2 = Task.Factory.StartNew(async () => { await cashCard.WithdrawAsync(625, 300M, timeOut.Add(timeOut)); });
            Task innerTask3 = Task.Factory.StartNew(async () => { await cashCard.WithdrawAsync(725, 400M, timeOut.Add(timeOut)); });

            var task = Task.Factory.ContinueWhenAll(
                new[] { innerTask1, innerTask2, innerTask3 },
                innerTasks =>
                {
                    foreach (var innerTask in innerTasks)
                        Assert.That(innerTask.IsFaulted, Is.False);
                    Assert.AreEqual(cashCard.Balance,decimal.Subtract(prebalance,800M));
                });
        }
    }
}
