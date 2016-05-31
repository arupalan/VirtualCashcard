# VirtualCashcard
This is a very basic virtual cash card implementation in C# using SOLID principle, which uses lambda and async await to simplyfy concurrency unit testing.

## Sample Mock functional injection:
```csharp
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
            var cashCard = new Cashcard(mockPinValidator.Object);
            var prebalance = cashCard.Balance;
            Assert.That( async () => await cashCard.TopupbalanceAsync(500, 200M, timeOut),
                Throws.TypeOf<TaskCanceledException>());
            Assert.AreEqual(prebalance, cashCard.Balance);
        }
```

## Sample Mock Parallel testing
```c#
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

            var cashCard = new Cashcard(mockPinValidator.Object);
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
```
## Requirements:
1.	Can withdraw money if a valid pin is supplied. The balance on the card needs to adjust accordingly.
2.	Can be topped up any time by an arbitrary amount.
3.	The cash card, being virtual, can be used in many places at the same time.

## Principles:
1.	Well tested code (test driven would be best)
2.	Write the code as you would write a part of a production grade system
3.	Requirements must be met but please don’t go overboard

## Technology 
1.  c# 4.5 async await (callback) feature 
2.  NUnit async support
3.  MOQ lambda for mock functional injection
4.  SOLID design (SRP => note only interface provided for IPinValidation as the intent is to inject real implementation) 

# Building From Source
1. Move to your local git repository directory or any directory (with git init) in console.

2. Clone repository.

        git clone https://github.com/arupalan/VirtualCashcard.git
        
3. Move to source directory, update submodule and build.

        cd VirtualCashcard/
        git submodule update --init --recursive
        msbuild

## Test and Coverage
* You can execute with switch -console to see the logs on console
 ![Console Mode](http://www.alanaamy.net/wp-content/uploads/2016/05/CashCardTestAndCover.png)
