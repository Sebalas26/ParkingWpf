using System;
using System.Collections.Generic;
using System.Text.Json;
using FluentAssertions;
using Parking.Core.Converters;
using Parking.Core.Enums;
using Parking.Entities;
using Parking.Models;
using Parking.Models.ApiModels;
using Parking.Services.Implementations;
using Xunit;

namespace Parking.UnitTests.Converters;

public class JsonSerializationTests
{
    [Theory]
    [InlineData("\"18:00\"", 18, 0, 0)]
    [InlineData("\"06:00\"", 6, 0, 0)]
    [InlineData("\"08:30\"", 8, 30, 0)]
    [InlineData("\"22:15:30\"", 22, 15, 30)]
    [InlineData("\"0.08:00:00\"", 8, 0, 0)]
    public void FlexibleTimeSpanJsonConverter_ReadsVariousStringFormatsCorrectly(string json, int h, int m, int s)
    {
        var options = new JsonSerializerOptions
        {
            Converters = { new FlexibleTimeSpanJsonConverter() }
        };

        var result = JsonSerializer.Deserialize<TimeSpan>(json, options);
        result.Should().Be(new TimeSpan(h, m, s));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"\"")]
    [InlineData("\"   \"")]
    public void NullableFlexibleTimeSpanJsonConverter_HandlesNullOrEmptySafely(string json)
    {
        var options = new JsonSerializerOptions
        {
            Converters = { new NullableFlexibleTimeSpanJsonConverter() }
        };

        var result = JsonSerializer.Deserialize<TimeSpan?>(json, options);
        result.Should().BeNull();
    }

    [Fact]
    public void BranchModel_Deserializes44RealisticBranchesWithoutJsonException()
    {
        var branchListJson = new List<string>();
        for (int i = 1; i <= 44; i++)
        {
            branchListJson.Add($@"{{
                ""id"": {i},
                ""companyId"": 1,
                ""name"": ""Sede {i}"",
                ""totalCapacity"": 100,
                ""nightStartTime"": ""18:00"",
                ""nightEndTime"": ""06:00"",
                ""fullDayStartTime"": ""08:00"",
                ""fullDayEndTime"": ""20:00"",
                ""operatingHours"": [
                    {{
                        ""id"": {i * 10},
                        ""branchId"": {i},
                        ""dayOfWeek"": 1,
                        ""isOpen"": true,
                        ""openingTime"": ""08:00"",
                        ""closingTime"": ""22:00""
                    }}
                ]
            }}");
        }

        var fullJson = "[" + string.Join(",", branchListJson) + "]";

        // Deserializar con las opciones oficiales de ParkingApiClient
        var branches = JsonSerializer.Deserialize<List<BranchModel>>(fullJson, ParkingApiClient.JsonOptions);

        branches.Should().NotBeNull();
        branches!.Count.Should().Be(44);
        branches[0].NightStartTime.Should().Be(new TimeSpan(18, 0, 0));
        branches[0].NightEndTime.Should().Be(new TimeSpan(6, 0, 0));
        branches[0].OperatingHours.Should().HaveCount(1);
        branches[0].OperatingHours[0].OpeningTime.Should().Be(new TimeSpan(8, 0, 0));
        branches[0].OperatingHours[0].ClosingTime.Should().Be(new TimeSpan(22, 0, 0));
    }

    [Fact]
    public void ApiBranchSyncDto_DeserializesShortTimeFormatsWithoutException()
    {
        var json = @"{
            ""id"": 1,
            ""name"": ""Plaza Central"",
            ""nightStartTime"": ""18:00"",
            ""nightEndTime"": ""06:00"",
            ""fullDayStartTime"": ""07:30"",
            ""fullDayEndTime"": ""19:45""
        }";

        var dto = JsonSerializer.Deserialize<ApiBranchSyncDto>(json, ParkingApiClient.JsonOptions);

        dto.Should().NotBeNull();
        dto!.NightStartTime.Should().Be(new TimeSpan(18, 0, 0));
        dto.NightEndTime.Should().Be(new TimeSpan(6, 0, 0));
        dto.FullDayStartTime.Should().Be(new TimeSpan(7, 30, 0));
        dto.FullDayEndTime.Should().Be(new TimeSpan(19, 45, 0));
    }

    [Theory]
    [InlineData(@"{""shiftId"":""11111111-1111-1111-1111-111111111111"",""status"":""Open"",""expectedCash"":150000}", 0)]
    [InlineData(@"{""shiftId"":""22222222-2222-2222-2222-222222222222"",""status"":""Closed"",""expectedCash"":200000}", 1)]
    [InlineData(@"{""shiftId"":""33333333-3333-3333-3333-333333333333"",""status"":0,""expectedCash"":100000}", 0)]
    [InlineData(@"{""shiftId"":""44444444-4444-4444-4444-444444444444"",""status"":1,""expectedCash"":300000}", 1)]
    public void ShiftSummaryModel_DeserializesStatusBothStringAndNumber(string json, int expectedStatus)
    {
        var summary = JsonSerializer.Deserialize<ShiftSummaryModel>(json, ParkingApiClient.JsonOptions);

        summary.Should().NotBeNull();
        summary!.Status.Should().Be(expectedStatus);
    }

    [Fact]
    public void OfflineQueue_DeserializesCheckInAndCheckOutWithEnumStrings()
    {
        var checkInJson = @"{
            ""plateNumber"": ""ABC123"",
            ""vehicleType"": ""Motorcycle"",
            ""hourlyRate"": 3000.00
        }";

        var checkOutJson = @"{
            ""ticketId"": ""11111111-1111-1111-1111-111111111111"",
            ""paymentMethod"": ""DebitCard"",
            ""amountPaid"": 15000.00
        }";

        var checkInReq = JsonSerializer.Deserialize<CheckInApiRequest>(checkInJson, ParkingApiClient.JsonOptions);
        var checkOutReq = JsonSerializer.Deserialize<CheckOutApiRequest>(checkOutJson, ParkingApiClient.JsonOptions);

        checkInReq.Should().NotBeNull();
        checkInReq!.VehicleType.Should().Be(VehicleType.Motorcycle);

        checkOutReq.Should().NotBeNull();
        checkOutReq!.PaymentMethod.Should().Be(PaymentMethod.DebitCard);
    }
}
