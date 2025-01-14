﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Host.Bindings.Path;
using Microsoft.Azure.WebJobs.Host.Tables;
using NUnit.Framework;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Tables
{
    public class TableFilterFormatterTests
    {
        [Test]
        public void Format_InvalidFilter_ThrowsExpectedException()
        {
            // verify an invalid integer value
            var template = BindingTemplate.FromString("Age gt {Age}");
            var bindingData = new Dictionary<string, object>
            {
                { "Age", "25 or true" }
            };
            var ex = Assert.Throws<InvalidOperationException>(() => TableFilterFormatter.Format(template, (IReadOnlyDictionary<string, object>)bindingData));
            Assert.AreEqual($"An invalid parameter value was specified for filter parameter 'Age'.", ex.Message);
            // verify the same template reversed
            template = BindingTemplate.FromString("{Age} lt Age");
            bindingData = new Dictionary<string, object>
            {
                { "Age", "25 or true" }
            };
            ex = Assert.Throws<InvalidOperationException>(() => TableFilterFormatter.Format(template, (IReadOnlyDictionary<string, object>)bindingData));
            Assert.AreEqual($"An invalid parameter value was specified for filter parameter 'Age'.", ex.Message);
            // verify datetime day operator value
            template = BindingTemplate.FromString("day({BirthDate}) eq 8");
            bindingData = new Dictionary<string, object>
            {
                { "BirthDate", "BirthDate) neq 8 or day(BirthDate" }
            };
            ex = Assert.Throws<InvalidOperationException>(() => TableFilterFormatter.Format(template, (IReadOnlyDictionary<string, object>)bindingData));
            Assert.AreEqual($"An invalid parameter value was specified for filter parameter 'BirthDate'.", ex.Message);
            // verify ambiguous values are handled correctly
            template = BindingTemplate.FromString("Age eq '{Age}' or Age eq {Age}");
            bindingData = new Dictionary<string, object>
            {
                { "Age", "25 or true" }
            };
            ex = Assert.Throws<InvalidOperationException>(() => TableFilterFormatter.Format(template, (IReadOnlyDictionary<string, object>)bindingData));
            Assert.AreEqual($"An invalid parameter value was specified for filter parameter 'Age'.", ex.Message);
        }

        [Test]
        public void Format_StringLiteral_ReturnsExpectedValue()
        {
            // verify single quotes are escaped
            var bindingData = new Dictionary<string, object>
            {
                { "Name", "x' or 'x' eq 'x" }
            };
            string result = GetFormattedValue("Name eq '{name}'", bindingData);
            Assert.AreEqual("Name eq 'x'' or ''x'' eq ''x'", result);
            // verify the same template reversed
            bindingData = new Dictionary<string, object>
            {
                { "Name", "x' or 'x' eq 'x" }
            };
            result = GetFormattedValue("'{name}' eq Name", bindingData);
            Assert.AreEqual("'x'' or ''x'' eq ''x' eq Name", result);
            // verify a multipart filter
            bindingData = new Dictionary<string, object>
            {
                { "Name", "O'Malley" },
                { "Age", "35" }
            };
            result = GetFormattedValue("(Age gt {Age}) and (Name eq '{Name}')", bindingData);
            Assert.AreEqual("(Age gt 35) and (Name eq 'O''Malley')", result);
            // datetime filter range
            bindingData = new Dictionary<string, object>
            {
                { "D1", "2000-01-10" },
                { "D2", "2017-01-10" }
            };
            result = GetFormattedValue("(BirthDate gt datetime'{D1}') and (BirthDate lt datetime'{D2}')", bindingData);
            Assert.AreEqual("(BirthDate gt datetime'2000-01-10') and (BirthDate lt datetime'2017-01-10')", result);
            // guid filter
            bindingData = new Dictionary<string, object>
            {
                { "Category", "Electronics" },
                { "ID", "110C91D4-5412-4DAC-A960-EF0BCB8BAFEB" }
            };
            result = GetFormattedValue("Category eq '{Category}' and ID eq guid'{ID}'", bindingData);
            Assert.AreEqual("Category eq 'Electronics' and ID eq guid'110C91D4-5412-4DAC-A960-EF0BCB8BAFEB'", result);
            // invalid guid filter containing a quote
            // we double quote it, but the value will fail to parse
            // in table service
            bindingData = new Dictionary<string, object>
            {
                { "ID", "invalid'value" }
            };
            result = GetFormattedValue("ID eq guid'{ID}'", bindingData);
            Assert.AreEqual("ID eq guid'invalid''value'", result);
            // binary filter
            bindingData = new Dictionary<string, object>
            {
                { "Value", "TWFyeSBoYWQgYSBsaXR0bGUgbGFtYiE=" }
            };
            result = GetFormattedValue("Binary eq X'{Value}'", bindingData);
            Assert.AreEqual("Binary eq X'TWFyeSBoYWQgYSBsaXR0bGUgbGFtYiE='", result);
            // verify that the entire filter can be specified as an expression
            bindingData = new Dictionary<string, object>
            {
                { "Filter", "Name eq 'Curly'" }
            };
            result = GetFormattedValue("{Filter}", bindingData);
            Assert.AreEqual("Name eq 'Curly'", result);
        }

        [Test]
        public void Format_MissingParameter_ThrowsExpectedException()
        {
            var bindingData = new Dictionary<string, object>
            {
                { "Category", "Electronics" }
            };
            var ex = Assert.Throws<InvalidOperationException>(() => { GetFormattedValue("Category eq '{Category}' and ID eq guid'{ID}'", bindingData); });
            Assert.AreEqual("No value for named parameter 'ID'.", ex.Message);
        }

        [Test]
        public void Format_SpecialTypes_ReturnsExpectedResult()
        {
            DateTime dateTime = new DateTime(2017, 1, 26, 19, 30, 00, DateTimeKind.Utc);
            DateTimeOffset dateTimeOffset = new DateTimeOffset(dateTime);
            Guid guid = new Guid("110c91d4-5412-4dac-a960-ef0bcb8bafeb");
            var bindingData = new Dictionary<string, object>
            {
                { "DT", dateTime },
                { "DTO", dateTimeOffset },
                { "ID", guid }
            };
            string result = GetFormattedValue("DateTime eq datetime'{DT}' and DateTimeOffset eq datetime'{DTO}' and ID eq guid'{ID}'", bindingData);
            Assert.AreEqual("DateTime eq datetime'2017-01-26T19:30:00.0000000Z' and DateTimeOffset eq datetime'2017-01-26T19:30:00.0000000Z' and ID eq guid'110c91d4-5412-4dac-a960-ef0bcb8bafeb'", result);
        }

        [Theory]
        [TestCase("True", true)]
        [TestCase("true", true)]
        [TestCase("false", true)]
        [TestCase("1", true)]
        [TestCase("0", true)]
        [TestCase("1234567", true)]
        [TestCase("-1234567", true)]
        [TestCase("12345.678", true)]
        [TestCase("-12345.678", true)]
        [TestCase("0.0", true)]
        [TestCase("2017-01-23T09:13:28", false)]
        [TestCase("2017-01-23T13:40:33.9300406-08:00", false)]
        [TestCase("2017-01-23", false)]
        [TestCase("110C91D4-5412-4DAC-A960-EF0BCB8BAFEB", false)] // binary (base 64)
        [TestCase("TWFyeSBoYWQgYSBsaXR0bGUgbGFtYiE=", false)]
        [TestCase("", false)]
        [TestCase("test value", false)]
        [TestCase("test'value", false)]
        [TestCase("test(value", false)]
        [TestCase("test)value", false)]
        [TestCase("BirthDate) neq 8 or day(BirthDate", false)]
        public void ValidateNonStringLiteral_ReturnsExpectedResult(string value, bool expectedResult)
        {
            bool result = TableFilterFormatter.TryValidateNonStringLiteral(value);
            Assert.AreEqual(expectedResult, result);
        }

        private static string GetFormattedValue(string templateString, Dictionary<string, object> bindingData)
        {
            var template = BindingTemplate.FromString(templateString);
            return TableFilterFormatter.Format(template, (IReadOnlyDictionary<string, object>)bindingData);
        }
    }
}