using System.ComponentModel.DataAnnotations;
using Qistas.Domain.Enums;
using Qistas.Domain.Models;

namespace Qistas.Api.Contracts;

/// <summary>
/// HTTP-facing request/response shapes for the Balance-facing endpoints. Kept separate
/// from both the D365 wire contracts (Qistas.Domain.Contracts) and the domain models
/// (Qistas.Domain.Models) -- Balance/.NET 3.5 talks plain JSON over localhost, it should
/// never need to know about D365's field-name quirks.
///
/// DataAnnotations attributes below guard the HTTP boundary (malformed/empty input) and
/// are enforced by <see cref="Qistas.Api.Validators.ValidationFilter{T}"/> before the
/// handler runs. Business-rule validation (license expiry, weight sanity, tolerance) stays
/// in <c>Qistas.Domain.Validation.D365Validation</c>.
/// </summary>
public sealed class EntryWeightRequestDto
{
    [Required, MinLength(1)]
    public required string LoadId { get; init; }

    [Required, MinLength(1)]
    public required string CompanyId { get; init; }

    /// <summary>Balance operator (login) name -- becomes the D365 "Userid".</summary>
    [Required, MinLength(1)]
    public required string OperatorUserId { get; init; }

    [Required, MinLength(1)]
    public required string ScaleSystemReferenceId { get; init; }

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    public required decimal EntryWeightKg { get; init; }

    public DateTimeOffset? EntryDateTimeUtc { get; init; }

    [Required, MinLength(1)]
    public required string DriverName { get; init; }

    [Required, MinLength(1)]
    public required string DriverNationalId { get; init; }

    [Required, MinLength(1)]
    public required string DriverLicenseId { get; init; }

    public required DateOnly DriverLicenseExpiryDate { get; init; }
    public string? DriverInternalId { get; init; }
    public bool IsInternalDriver { get; init; }

    /// <summary>Balance's TruckNo -- becomes the D365 "VehiclePlateNumber".</summary>
    [Required, MinLength(1)]
    public required string VehiclePlateNumber { get; init; }

    [Required, MinLength(1)]
    public required string VehicleLicenseId { get; init; }

    public required DateOnly VehicleLicenseExpiryDate { get; init; }
    public VehicleType? VehicleType { get; init; }
    public bool IsInternalVehicle { get; init; }
    public string? VehicleNote { get; init; }

    public static EntryWeightSubmission ToDomain(EntryWeightRequestDto dto) => new()
    {
        LoadId = dto.LoadId,
        CompanyId = dto.CompanyId,
        UserId = dto.OperatorUserId,
        ScaleSystemReferenceId = dto.ScaleSystemReferenceId,
        EntryWeightKg = dto.EntryWeightKg,
        EntryDateTimeUtc = dto.EntryDateTimeUtc ?? DateTimeOffset.UtcNow,
        Driver = new DriverInfo
        {
            DriverName = dto.DriverName,
            NationalId = dto.DriverNationalId,
            LicenseId = dto.DriverLicenseId,
            LicenseExpiryDate = dto.DriverLicenseExpiryDate,
            InternalId = dto.DriverInternalId,
            IsInternal = dto.IsInternalDriver,
        },
        Vehicle = new VehicleInfo
        {
            PlateNumber = dto.VehiclePlateNumber,
            LicenseId = dto.VehicleLicenseId,
            LicenseExpiryDate = dto.VehicleLicenseExpiryDate,
            VehicleType = dto.VehicleType,
            IsInternal = dto.IsInternalVehicle,
            Note = dto.VehicleNote,
        },
    };
}

public sealed class ExitWeightRequestDto
{
    [Required, MinLength(1)]
    public required string LoadId { get; init; }

    [Required, MinLength(1)]
    public required string CompanyId { get; init; }

    /// <summary>Balance operator (login) name -- becomes the D365 "Userid".</summary>
    [Required, MinLength(1)]
    public required string OperatorUserId { get; init; }

    [Required, MinLength(1)]
    public required string ScaleSystemReferenceId { get; init; }

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    public required decimal EntryWeightKg { get; init; }

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    public required decimal ExitWeightKg { get; init; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public required decimal TotalNetWeightKg { get; init; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public required decimal TotalGrossWeightKg { get; init; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public required decimal ToleranceKg { get; init; }

    public DateTimeOffset? ExitDateTimeUtc { get; init; }

    public static ExitWeightSubmission ToDomain(ExitWeightRequestDto dto) => new()
    {
        LoadId = dto.LoadId,
        CompanyId = dto.CompanyId,
        UserId = dto.OperatorUserId,
        ScaleSystemReferenceId = dto.ScaleSystemReferenceId,
        EntryWeightKg = dto.EntryWeightKg,
        ExitWeightKg = dto.ExitWeightKg,
        TotalNetWeightKg = dto.TotalNetWeightKg,
        TotalGrossWeightKg = dto.TotalGrossWeightKg,
        ToleranceKg = dto.ToleranceKg,
        ExitDateTimeUtc = dto.ExitDateTimeUtc ?? DateTimeOffset.UtcNow,
    };
}

public sealed class ApiResultDto
{
    public required bool Success { get; init; }
    public string? Message { get; init; }
    public bool WasAlreadyProcessed { get; init; }

    public static ApiResultDto From(D365OperationResult result) => new()
    {
        Success = result.Success,
        Message = result.Message,
        WasAlreadyProcessed = result.WasAlreadyProcessed,
    };
}

public sealed class LoadValidationResultDto
{
    public required bool Success { get; init; }
    public string? Message { get; init; }
    public string? LoadId { get; init; }
    public string? CompanyId { get; init; }
    public decimal? HeaderNetWeightKg { get; init; }
    public decimal? HeaderGrossWeightKg { get; init; }
    public decimal TotalLineNetWeightKg { get; init; }
    public decimal TotalLineGrossWeightKg { get; init; }
    public IReadOnlyList<LoadLineInfo> Lines { get; init; } = [];

    /// <summary>D365's canonical driver master data, for Balance to sync its local driver
    /// records. Null when D365 did not echo driver details.</summary>
    public DriverDetailsResult? Driver { get; init; }

    /// <summary>D365's canonical vehicle master data, for Balance to sync its local vehicle
    /// records. Null when D365 did not echo vehicle details.</summary>
    public VehicleDetailsResult? Vehicle { get; init; }

    public static LoadValidationResultDto From(LoadValidationResult result) => new()
    {
        Success = result.Success,
        Message = result.Message,
        LoadId = result.LoadId,
        CompanyId = result.CompanyId,
        HeaderNetWeightKg = result.HeaderNetWeightKg,
        HeaderGrossWeightKg = result.HeaderGrossWeightKg,
        TotalLineNetWeightKg = result.TotalLineNetWeightKg,
        TotalLineGrossWeightKg = result.TotalLineGrossWeightKg,
        Lines = result.Lines,
        Driver = result.Driver,
        Vehicle = result.Vehicle,
    };
}
