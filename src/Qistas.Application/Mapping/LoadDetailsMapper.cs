using Qistas.Domain.Contracts;
using Qistas.Domain.Models;
using Qistas.Domain.Validation;

namespace Qistas.Application.Mapping;

/// <summary>
/// Maps the common D365 response (Context.LoadHeader / Context.LoadLines, with the
/// Vehichle* typo'd header fields) into the clean domain <see cref="LoadValidationResult"/>,
/// normalizing every weight to KG (Balance/CLAUDE.md #16.11).
/// </summary>
public static class LoadDetailsMapper
{
    public static LoadValidationResult ToDomainResult(D365Response? response, string? transportError)
    {
        if (response is null)
        {
            return new LoadValidationResult { Success = false, Message = transportError ?? "No response from D365." };
        }

        var header = response.Context?.LoadHeader;
        if (!response.Status || header is null)
        {
            return new LoadValidationResult { Success = false, Message = response.Message ?? "Load not found." };
        }

        var lines = (response.Context?.LoadLines ?? new List<LoadLine>()).Select(line => new LoadLineInfo
        {
            ItemId = line.ItemId,
            ItemName = line.ItemName,
            ItemDescription = line.ItemDescription,
            BatchNumber = line.BatchNumber,
            QuantityKg = WeightUnitConverter.ToKg(line.Qty, line.UnitId),
            NetWeightKg = WeightUnitConverter.ToKg(line.ItemNetWeight, line.UnitId),
            GrossWeightKg = WeightUnitConverter.ToKg(line.ItemGrossWeight, line.UnitId),
        }).ToList();

        return new LoadValidationResult
        {
            Success = true,
            Message = response.Message,
            LoadId = header.LoadId,
            // CompanyId lives at the top level of the common response (may differ in
            