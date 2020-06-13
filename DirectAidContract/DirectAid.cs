using Stratis.SmartContracts;

[Deploy]
public class DirectAid : SmartContract
{
    public DirectAid(ISmartContractState smartContractState, ulong startTime, ulong endTime)
        : base(smartContractState)
    {
        Owner = Message.Sender;
        StartTime = startTime;
        EndTime = endTime;
    }

    private const string PendingStatus = "PENDING";
    private const string ApprovedStatus = "APPROVED";
    private const string RejectStatus = "REJECTED";

    public Address Owner
    {
        get => PersistentState.GetAddress(nameof(Owner));
        private set => PersistentState.SetAddress(nameof(Owner), value);
    }
    
    public ulong StartTime
    {
        get => PersistentState.GetUInt64(nameof(StartTime));
        private set => PersistentState.SetUInt64(nameof(StartTime), value);
    }

    public ulong EndTime
    {
        get => PersistentState.GetUInt64(nameof(EndTime));
        private set => PersistentState.SetUInt64(nameof(EndTime), value);
    }

    private void SetCategoryAmount(int categoryType, ulong amount)
    {
        PersistentState.SetUInt64($"Category:{categoryType}", amount);
    }

    private ulong GetCategoryAmount(int categoryType)
    {
        return PersistentState.GetUInt64($"Category:{categoryType}");
    }

    private void SetApplicationStatus(string applicationId, string status)
    {
        PersistentState.SetString($"Status:{applicationId}", status);
    }

    public string GetApplicationStatus(string applicationId)
    {
        return PersistentState.GetString($"Status:{applicationId}");
    }

    private void SetApplicantAddress(string id, Address address)
    {
        PersistentState.SetAddress($"{id}", address);
    }

    private Address GetApplicantAddress(string id)
    {
        return PersistentState.GetAddress($"{id}");
    }

    private int GetApplicationCategory(string id)
    {
        return PersistentState.GetInt32($"{id}:Category");
    }

    private void SetApplicationCategory(string id, ApplicationCategory category)
    {
        PersistentState.SetInt32($"{id}:Category", (int)category);
    }

    private void Enroll(Address address, bool value)
    {
        PersistentState.SetBool($"{address}:Enroll", value);
    }

    public bool IsEnrolled(Address address)
    {
        return PersistentState.GetBool($"{address}:Enroll");
    }

    public bool AddFundToContract()
    {
        Assert(Message.Sender == Owner);
        Assert(Message.Value > 0);

        this.Transfer(this.Address, Message.Value);

        Log(new TransactionLog { From = Message.Sender, To = Address, Amount = Message.Value });

        return true;
    }

    public bool SetAmountForCategory(int categoryType, ulong amount)
    {
        Assert(Message.Sender == Owner);
        Assert(amount > 0);

        ulong amountInSatoshis = amount * 100_000_000;
        Assert(this.Balance > amountInSatoshis);

        SetCategoryAmount(categoryType, amountInSatoshis);

        return true;
    }

    public bool Approve(string applicationId)
    {
        Assert(Message.Sender == Owner);

        var applicant = GetApplicantAddress(applicationId);

        var amountToTransfer = GetCategoryAmount(GetApplicationCategory(applicationId));

        Assert(amountToTransfer > 0);

        Assert(Balance > amountToTransfer, "Not enough funds");

        var transferResult = Transfer(applicant, amountToTransfer);

        Assert(transferResult.Success, $"Transfer failure.");

        SetApplicationStatus(applicationId, ApprovedStatus);

        Log(new TransactionLog { From = this.Address, To = applicant, Amount = amountToTransfer });

        Log(new StatusLog { Id = applicationId, Applicant = applicant, Status = ApprovedStatus });

        return true;
    }

    public bool Reject(string applicationId)
    {
        Assert(Message.Sender == Owner);
        SetApplicationStatus(applicationId, RejectStatus);

        var address = GetApplicantAddress(applicationId);
        Enroll(address, false);

        Log(new StatusLog { Id = applicationId, Applicant = address, Status = RejectStatus });

        return true;
    }

    public bool SubmitApplication(string applicationId, int category, ulong currentTime)
    {
        Assert(Message.Sender != Owner, "Sender cannot be owner.");

        bool invalidApplicationStatus = !string.IsNullOrEmpty(GetApplicationStatus(applicationId));

        if (invalidApplicationStatus)
        {
            return false;
        }

        Assert(currentTime > StartTime && currentTime < EndTime);

        Assert(!IsEnrolled(Message.Sender), "Already enrolled.");

        RegisterApplicationExecute(applicationId, Message.Sender, category);

        Log(new StatusLog { Id = applicationId, Applicant = Message.Sender, Status = PendingStatus });

        return true;
    }

    private void RegisterApplicationExecute(string applicationId, Address applicant, int category)
    {
        Enroll(applicant, true);

        SetApplicantAddress(applicationId, applicant);
        SetApplicationStatus(applicationId, PendingStatus);
        SetApplicationCategory(applicationId, (ApplicationCategory)category);
    }

    public enum ApplicationCategory
    {
        SmallBusiness = 1,
        MediumBusiness = 2,
        Farmer = 3,
        SeniorCitizen = 4,
        HealthWorker = 5,
        DailyWageWorker = 6
    }

    #region Events

    public struct StatusLog
    {
        [Index]
        public string Id;
        [Index]
        public Address Applicant;

        public string Status;
    }

    public struct TransactionLog
    {
        [Index]
        public Address From;

        [Index]
        public Address To;

        public ulong Amount;
    }

    #endregion
}
