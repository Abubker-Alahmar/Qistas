namespace Qistas.Api.Validators;

// Intentionally empty: ExitWeightRequestDto validation rules live as
// System.ComponentModel.DataAnnotations attributes directly on
// Qistas.Api.Contracts.ExitWeightRequestDto and are enforced by ValidationFilter<T>
// (see ValidationFilter.cs). See EntryWeightRequestDtoValidator.cs for why a
// FluentValidation-based validator class was not used.
