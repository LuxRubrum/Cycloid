namespace Cycloids.ViewModel.Models;

/// <summary>Прямоугольник сцены в мировых координатах (метры, Y вверх), который должен поместиться на графике.</summary>
public sealed record SceneBounds(double XMin, double XMax, double YMin, double YMax);
