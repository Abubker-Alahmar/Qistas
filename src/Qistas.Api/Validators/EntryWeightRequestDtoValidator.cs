namespace Qistas.Api.Validators;

// Intentionally empty: EntryWeightRequestDto validation rules live as
// System.ComponentModel.DataAnnotations attributes directly on
// Qistas.Api.Contracts.EntryWeightRequestDto and are enforced by ValidationFilter<T>
// (see ValidationFilter.cs). A separate FluentValidation validator class was not used
// here because this sandbox cannot restore new NuGet packages from nuget.org (only
// Ubuntu package mirrors and github.com are reachable), so the fix uses the built-in
// DataAnnotations validator instead. This file is kept (rather than deleted) because the
// sandbox filesystem mount does not permit file deletion.
