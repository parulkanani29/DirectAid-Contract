namespace DirectAidContract.Tests
{
    using Moq;
    using Stratis.SmartContracts;
    using Stratis.SmartContracts.CLR;
    using Xunit;

    using static DirectAid;

    public class DirectAidTests
    {
        private readonly Mock<ISmartContractState> MockContractState;
        private readonly Mock<IPersistentState> MockPersistentState;
        private readonly Mock<IContractLogger> MockContractLogger;
        private readonly Mock<IInternalTransactionExecutor> MockInternalExecutor;
        private readonly Address Owner;
        private readonly Address Applicant;
        private readonly Address ApplicantTwo;
        private readonly Address ContractAddress;

        private const string PendingStatus = "PENDING";
        private const string ApprovedStatus = "APPROVED";
        private const string RejectStatus = "REJECTED";
        private const string ApplicationId = "57901a70-6996-4335-9fba-4cbf9e31713b";
        private const int ApplicationDefaultCategory = (int)ApplicationCategory.DailyWageWorker;
        private const ulong StartTime = 1590215400; // May 23, 2020 6:30:00 AM
        private const ulong EndTime = 1595485800; // July 23, 2020 6:30:00 AM
        private const ulong CurrentTime = 1590339635; //May 24, 2020 5:00:35 PM
        private const ulong InitialFund = 100_00_000_000;

        public DirectAidTests()
        {
            this.MockContractLogger = new Mock<IContractLogger>();
            this.MockPersistentState = new Mock<IPersistentState>();
            this.MockContractState = new Mock<ISmartContractState>();
            this.MockInternalExecutor = new Mock<IInternalTransactionExecutor>();
            this.MockContractState.Setup(x => x.PersistentState).Returns(MockPersistentState.Object);
            this.MockContractState.Setup(x => x.ContractLogger).Returns(MockContractLogger.Object);
            this.MockContractState.Setup(x => x.InternalTransactionExecutor).Returns(MockInternalExecutor.Object);
            this.Owner = "0x0000000000000000000000000000000000000001".HexToAddress();
            this.Applicant = "0x0000000000000000000000000000000000000002".HexToAddress();
            this.ApplicantTwo = "0x0000000000000000000000000000000000000003".HexToAddress();
            this.ContractAddress = "0x0000000000000000000000000000000000000004".HexToAddress();
        }

        private DirectAid Newcontract(Address sender, Address owner, ulong value, ulong startTime, ulong endTime)
        {
            MockContractState.Setup(x => x.Message).Returns(new Message(ContractAddress, sender, value));
            MockPersistentState.Setup(x => x.GetAddress(nameof(Owner))).Returns(owner);
            MockPersistentState.Setup(x => x.GetUInt64(nameof(StartTime))).Returns(startTime);
            MockPersistentState.Setup(x => x.GetUInt64(nameof(EndTime))).Returns(endTime);

            return new DirectAid(this.MockContractState.Object, StartTime, EndTime);
        }

        [Fact]
        public void Constructor_Sets_Properties()
        {
            var contract = Newcontract(Owner, Owner, 0, StartTime, EndTime);
            Assert.Equal(Owner, contract.Message.Sender);
            Assert.Equal(StartTime, contract.StartTime);
            Assert.Equal(EndTime, contract.EndTime);
        }

        [Theory]
        [InlineData(InitialFund)]
        public void AddFundToContract_Succeeds(ulong amount)
        {
            var contract = Newcontract(Owner, Owner, amount, StartTime, EndTime);

            MockInternalExecutor.Setup(s =>
                s.Transfer(
                    MockContractState.Object,
                    ContractAddress,
                    amount))
                .Returns(TransferResult.Transferred(new object()));

            var result = contract.AddFundToContract();

            Assert.True(result);

            MockInternalExecutor.Verify(s => s.Transfer(MockContractState.Object, ContractAddress, amount), Times.Once);
            MockContractLogger.Verify(x => x.Log(It.IsAny<ISmartContractState>(), new TransactionLog { From = Owner, To = ContractAddress, Amount = amount }), Times.Once);
        }

        [Theory]
        [InlineData(InitialFund)]
        public void AddFundToContract_Fails_If_Caller_IsNot_Owner(ulong amount)
        {
            var contract = Newcontract(Applicant, Owner, amount, StartTime, EndTime);

            Assert.Throws<SmartContractAssertException>(() => contract.AddFundToContract());
        }

        [Theory]
        [InlineData(0)]
        public void AddFundToContract_Fails_If_MessageValueIs_0(ulong amount)
        {
            var contract = Newcontract(Owner, Owner, amount, StartTime, EndTime);

            Assert.Throws<SmartContractAssertException>(() => contract.AddFundToContract());
        }

        [Theory]
        [InlineData(InitialFund)]
        public void ContractBalance_Equals_TransferredAmount(ulong amount)
        {
            var contract = Newcontract(Owner, Owner, amount, StartTime, EndTime);
            MockContractState.Setup(x => x.GetBalance).Returns(() => amount);

            Assert.Equal(amount, contract.Balance);
        }

        [Theory]
        [InlineData(0, 10)]
        public void SetAmountForCategory_Succeeds(int categoryType, ulong amount)
        {
            MockPersistentState.Setup(s => s.GetUInt64($"Category:{categoryType}")).Returns(amount);

            var contract = Newcontract(Owner, Owner, amount, StartTime, EndTime);
            MockContractState.Setup(x => x.GetBalance).Returns(() => InitialFund);

            var result = contract.SetAmountForCategory(categoryType, amount);
            Assert.True(result);

            var amountInStratoshis = amount * 100_000_000;

            MockPersistentState.Verify(x => x.SetUInt64($"Category:{categoryType}", amountInStratoshis), Times.Once);
        }

        [Theory]
        [InlineData(0, 10)]
        public void SetAmountForCategory_Fails_If_MessageValueIs_0(int categoryType, ulong amount)
        {
            var contract = Newcontract(Applicant, Owner, amount, StartTime, EndTime);

            Assert.Throws<SmartContractAssertException>(() => contract.SetAmountForCategory(categoryType, amount));
        }

        [Theory]
        [InlineData(0, 0)]
        public void SetAmountForCategory_Fails_If_Caller_IsNot_Owner(int categoryType, ulong amount)
        {
            var contract = Newcontract(Owner, Owner, amount, StartTime, EndTime);
            Assert.Throws<SmartContractAssertException>(() => contract.SetAmountForCategory(categoryType, amount));
        }

        [Theory]
        [InlineData(0, 10)]
        public void SetAmountForCategory_Fails_If_ContractBalance_Is_Less(int categoryType, ulong amount)
        {
            var contract = Newcontract(Owner, Owner, amount, StartTime, EndTime);
            MockContractState.Setup(x => x.GetBalance).Returns(() => InitialFund);

            var amountGreterThanContractBalance = amount + InitialFund;
            Assert.Throws<SmartContractAssertException>(() => contract.SetAmountForCategory(categoryType, amountGreterThanContractBalance));
        }

        [Fact]
        public void SubmitApplication_Succeeds()
        {
            var contract = Newcontract(Applicant, Owner, 0, StartTime, EndTime);
            MockPersistentState.Setup(s => s.GetString($"Status:{ApplicationId}")).Returns(string.Empty);
            MockPersistentState.Setup(s => s.GetBool($"{Applicant}:Enroll")).Returns(false);
            MockPersistentState.Setup(s => s.GetUInt64($"StartTime:{ApplicationId}")).Returns(StartTime);
            MockPersistentState.Setup(s => s.GetUInt64($"EndTime:{ApplicationId}")).Returns(EndTime);

            var result = contract.SubmitApplication(ApplicationId, ApplicationDefaultCategory, CurrentTime);

            Assert.True(result);

            MockPersistentState.Verify(x => x.SetBool($"{Applicant}:Enroll", true), Times.Once);
            MockPersistentState.Verify(x => x.SetString($"Status:{ApplicationId}", PendingStatus), Times.Once);
            MockPersistentState.Verify(x => x.SetAddress($"{ApplicationId}", Applicant), Times.Once);
            MockPersistentState.Verify(x => x.SetInt32($"{ApplicationId}:Category", ApplicationDefaultCategory), Times.Once);
            MockContractLogger.Verify(x => x.Log(It.IsAny<ISmartContractState>(), new StatusLog { Id = ApplicationId, Applicant = Applicant, Status = PendingStatus }), Times.Once);
        }

        [Fact]
        public void SubmitApplication_Fails_If_Caller_Is_Owner()
        {
            var contract = Newcontract(Owner, Owner, 0, StartTime, EndTime);
            Assert.Throws<SmartContractAssertException>(() => contract.SubmitApplication(ApplicationId, ApplicationDefaultCategory, CurrentTime));
        }

        [Theory]
        [InlineData(100, 1000)]
        public void Approve_Succeeds(ulong amountToTransfer, ulong contractBalance)
        {
            var contract = Newcontract(Owner, Owner, 0, StartTime, EndTime);
            MockPersistentState.Setup(s => s.GetString($"Status:{ApplicationId}")).Returns(ApprovedStatus);
            MockPersistentState.Setup(x => x.GetInt32($"{ApplicationId}:Category")).Returns(ApplicationDefaultCategory);
            MockPersistentState.Setup(s => s.GetAddress($"{ApplicationId}")).Returns(Applicant);
            MockPersistentState.Setup(s => s.GetUInt64($"Category:{ApplicationDefaultCategory}")).Returns(amountToTransfer);
            MockContractState.Setup(x => x.GetBalance).Returns(() => contractBalance);

            MockInternalExecutor.Setup(s =>
                s.Transfer(
                    MockContractState.Object,
                    Applicant,
                    amountToTransfer))
                .Returns(TransferResult.Transferred(new object()));

            var result = contract.Approve(ApplicationId);

            Assert.True(result);

            MockPersistentState.Verify(x => x.SetString($"Status:{ApplicationId}", ApprovedStatus), Times.Once);
            MockContractLogger.Verify(x => x.Log(It.IsAny<ISmartContractState>(), new StatusLog { Id = ApplicationId, Applicant = Applicant, Status = ApprovedStatus }), Times.Once);
            MockContractLogger.Verify(x => x.Log(It.IsAny<ISmartContractState>(), new TransactionLog { From = ContractAddress, To = Applicant, Amount = amountToTransfer }), Times.Once);
        }

        [Fact]
        public void Approve_Fails_If_Caller_IsNot_Owner()
        {
            var contract = Newcontract(Applicant, Owner, 0, StartTime, EndTime);
            Assert.Throws<SmartContractAssertException>(() => contract.Approve(ApplicationId));
        }

        [Theory]
        [InlineData(0)]
        public void Approve_Fails_If_AmountToTransfer_Is_0(ulong amountToTransfer)
        {
            var contract = Newcontract(Owner, Owner, 0, StartTime, EndTime);
            MockPersistentState.Setup(s => s.GetString($"Status:{ApplicationId}")).Returns(ApprovedStatus);
            MockPersistentState.Setup(s => s.GetUInt64($"StartTime:{ApplicationId}")).Returns(StartTime);
            MockPersistentState.Setup(s => s.GetUInt64($"EndTime:{ApplicationId}")).Returns(EndTime);
            MockPersistentState.Setup(s => s.GetAddress($"{ApplicationId}")).Returns(Applicant);
            MockPersistentState.Setup(x => x.GetInt32($"{ApplicationId}:Category")).Returns(ApplicationDefaultCategory);
            MockPersistentState.Setup(s => s.GetUInt64($"Category:{ApplicationDefaultCategory}")).Returns(amountToTransfer);

            Assert.Throws<SmartContractAssertException>(() => contract.Approve(ApplicationId));
        }

        [Theory]
        [InlineData(100, 10)]
        public void Approve_Fails_If_ContractBalance_Is_LessThan_AmountToTransfer(ulong amountToTransfer, ulong contractBalance)
        {
            var contract = Newcontract(Owner, Owner, 0, StartTime, EndTime);
            MockPersistentState.Setup(s => s.GetString($"Status:{ApplicationId}")).Returns(ApprovedStatus);
            MockPersistentState.Setup(s => s.GetUInt64($"StartTime:{ApplicationId}")).Returns(StartTime);
            MockPersistentState.Setup(s => s.GetUInt64($"EndTime:{ApplicationId}")).Returns(EndTime);
            MockPersistentState.Setup(s => s.GetAddress($"{ApplicationId}")).Returns(Applicant);
            MockPersistentState.Setup(x => x.GetInt32($"{ApplicationId}:Category")).Returns(ApplicationDefaultCategory);
            MockPersistentState.Setup(s => s.GetUInt64($"Category:{ApplicationDefaultCategory}")).Returns(amountToTransfer);
            MockContractState.Setup(x => x.GetBalance).Returns(() => contractBalance);

            Assert.Throws<SmartContractAssertException>(() => contract.Approve(ApplicationId));
        }

        [Fact]
        public void Reject_Succeeds()
        {
            var contract = Newcontract(Owner, Owner, 0, StartTime, EndTime);
            MockPersistentState.Setup(s => s.GetString($"Status:{ApplicationId}")).Returns(RejectStatus);
            MockPersistentState.Setup(s => s.GetBool($"{Applicant}:Enroll")).Returns(false);
            MockPersistentState.Setup(s => s.GetAddress($"{ApplicationId}")).Returns(Applicant);

            var result = contract.Reject(ApplicationId);
            Assert.True(result);

            MockPersistentState.Verify(x => x.SetString($"Status:{ApplicationId}", RejectStatus), Times.Once);
            MockPersistentState.Verify(x => x.SetBool($"{Applicant}:Enroll", false), Times.Once);
        }

        [Fact]
        public void Reject_Fails_If_Caller_IsNot_Owner()
        {
            var contract = Newcontract(Applicant, Owner, 0, StartTime, EndTime);
            Assert.Throws<SmartContractAssertException>(() => contract.Reject(ApplicationId));
        }
    }
}
