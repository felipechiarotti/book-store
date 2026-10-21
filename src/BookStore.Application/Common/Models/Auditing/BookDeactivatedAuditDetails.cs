namespace BookStore.Application.Common.Models.Auditing;

internal sealed record BookDeactivatedAuditDetails(bool PreviousIsActive, bool IsActive);