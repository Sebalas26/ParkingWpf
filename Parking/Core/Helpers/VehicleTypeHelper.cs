using System;
using System.Text.Json;
using Parking.Core.Enums;

namespace Parking.Core.Helpers;

public static class VehicleTypeHelper
{
    public static VehicleType Parse(object? rawVehicleType, string? fallbackName = null)
    {
        // 0. Si el nombre descriptivo (fallbackName) especifica explícitamente una categoría (ej: Patineta, Moto, etc.),
        // priorizarlo para evitar que un valor 0 (Car) por defecto del backend lo reclasifique erróneamente como automóvil.
        if (!string.IsNullOrWhiteSpace(fallbackName))
        {
            var cleanFallback = fallbackName.Trim().ToLowerInvariant();
            if (cleanFallback.Contains("patin") || cleanFallback.Contains("scooter") || cleanFallback.Contains("monopatin") || cleanFallback.Contains("monopatín") || cleanFallback.Contains("bici") || cleanFallback.Contains("cicla") || cleanFallback.Contains("bike"))
            {
                return VehicleType.Bicycle;
            }
            if (cleanFallback.Contains("moto") || cleanFallback.Contains("moped") || cleanFallback.Contains("cuatri") || cleanFallback.Contains("mototaxi") || cleanFallback.Contains("ciclomotor"))
            {
                return VehicleType.Motorcycle;
            }
            if (cleanFallback.Contains("camion") || cleanFallback.Contains("camión") || cleanFallback.Contains("truck") || cleanFallback.Contains("pesado") || cleanFallback.Contains("mula") || cleanFallback.Contains("volqueta") || cleanFallback.Contains("bus") || cleanFallback.Contains("trailer") || cleanFallback.Contains("tráiler"))
            {
                return VehicleType.HeavyTruck;
            }
            if (cleanFallback.Contains("suv") || cleanFallback.Contains("camioneta") || cleanFallback.Contains("campero") || cleanFallback.Contains("4x4") || cleanFallback.Contains("pickup"))
            {
                return VehicleType.Suv;
            }
            if (cleanFallback.Contains("van") || cleanFallback.Contains("furgon") || cleanFallback.Contains("furgón") || cleanFallback.Contains("micro") || cleanFallback.Contains("combi"))
            {
                return VehicleType.Van;
            }
        }

        // 1. Direct Integer / Numeric evaluation
        if (rawVehicleType is int intVal && Enum.IsDefined(typeof(VehicleType), intVal))
        {
            return (VehicleType)intVal;
        }

        if (rawVehicleType is JsonElement elem)
        {
            if (elem.ValueKind == JsonValueKind.Number && Enum.IsDefined(typeof(VehicleType), elem.GetInt32()))
            {
                return (VehicleType)elem.GetInt32();
            }

            if (elem.ValueKind == JsonValueKind.String)
            {
                var strVal = elem.GetString();
                if (!string.IsNullOrWhiteSpace(strVal))
                {
                    return ParseString(strVal, fallbackName);
                }
            }
        }

        if (rawVehicleType is string s && !string.IsNullOrWhiteSpace(s))
        {
            return ParseString(s, fallbackName);
        }

        if (!string.IsNullOrWhiteSpace(fallbackName))
        {
            return ParseString(fallbackName, null);
        }

        return VehicleType.Car;
    }

    private static VehicleType ParseString(string input, string? fallbackName)
    {
        var clean = input.Trim().ToLowerInvariant();

        if (clean.Contains("moto") || clean.Contains("scooter") || clean.Contains("moped") || clean.Contains("cuatri") || clean.Contains("ciclomotor") || clean.Contains("mototaxi"))
            return VehicleType.Motorcycle;

        if (clean.Contains("bici") || clean.Contains("bike") || clean.Contains("cicla") || clean.Contains("patin") || clean.Contains("monopatin") || clean.Contains("monopatín"))
            return VehicleType.Bicycle;

        if (clean.Contains("camion") || clean.Contains("camión") || clean.Contains("truck") || clean.Contains("pesado") || clean.Contains("mula") || clean.Contains("volqueta") || clean.Contains("bus") || clean.Contains("buseta") || clean.Contains("colectivo") || clean.Contains("trailer") || clean.Contains("tráiler"))
            return VehicleType.HeavyTruck;

        if (clean.Contains("suv") || clean.Contains("camioneta") || clean.Contains("campero") || clean.Contains("4x4") || clean.Contains("pickup") || clean.Contains("pick-up"))
            return VehicleType.Suv;

        if (clean.Contains("van") || clean.Contains("furgon") || clean.Contains("furgón") || clean.Contains("micro") || clean.Contains("combi") || clean.Contains("panel"))
            return VehicleType.Van;

        if (clean.Contains("car") || clean.Contains("auto") || clean.Contains("sedan") || clean.Contains("sedán") || clean.Contains("automovil") || clean.Contains("automóvil") || clean.Contains("coupe") || clean.Contains("hatchback") || clean.Contains("particular") || clean.Contains("vehiculo") || clean.Contains("vehículo"))
            return VehicleType.Car;

        if (Enum.TryParse<VehicleType>(input.Trim(), true, out var parsed))
            return parsed;

        if (!string.IsNullOrWhiteSpace(fallbackName))
        {
            var fallbackClean = fallbackName.Trim().ToLowerInvariant();
            if (fallbackClean.Contains("moto") || fallbackClean.Contains("scooter") || fallbackClean.Contains("cuatri")) return VehicleType.Motorcycle;
            if (fallbackClean.Contains("bici") || fallbackClean.Contains("bike") || fallbackClean.Contains("cicla") || fallbackClean.Contains("patin")) return VehicleType.Bicycle;
            if (fallbackClean.Contains("camion") || fallbackClean.Contains("truck") || fallbackClean.Contains("pesado") || fallbackClean.Contains("mula") || fallbackClean.Contains("volqueta") || fallbackClean.Contains("bus")) return VehicleType.HeavyTruck;
            if (fallbackClean.Contains("suv") || fallbackClean.Contains("camioneta") || fallbackClean.Contains("campero") || fallbackClean.Contains("4x4") || fallbackClean.Contains("pickup")) return VehicleType.Suv;
            if (fallbackClean.Contains("van") || fallbackClean.Contains("furgon") || fallbackClean.Contains("micro") || fallbackClean.Contains("combi")) return VehicleType.Van;
            if (fallbackClean.Contains("car") || fallbackClean.Contains("auto") || fallbackClean.Contains("sedan") || fallbackClean.Contains("automovil") || fallbackClean.Contains("particular")) return VehicleType.Car;
        }

        return VehicleType.Car;
    }
}
