namespace PortfolioManager.Domain.Entities;

public enum LedgerSide
{
    Debit,
    Credit
}

public enum LedgerTransactionType
{
    Buy,
    Sell,
    TransferIn,
    TransferOut,
    Deposit,
    Withdrawal,
    Fee,
    Dividend,
    Interest,
    Correction
}

public enum LedgerStatus
{
    Pending,
    Completed,
    Failed,
    Cancelled
}