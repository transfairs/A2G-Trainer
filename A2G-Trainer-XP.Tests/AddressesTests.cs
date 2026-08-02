using System;
using System.Collections.Generic;
using A2G_Trainer_XP.Model;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>Tests for the Addresses map's lookup and offset-shifting behavior.</summary>
    public class AddressesTests
    {
        private enum Key
        {
            First,
            Second,
            Third
        }

        [Fact]
        public void Create_ExposesValuesByKey()
        {
            Addresses addresses = Addresses.Create(
                new KeyValuePair<Enum, string>(Key.First, "10"),
                new KeyValuePair<Enum, string>(Key.Second, "20"));

            Assert.Equal("10", addresses[Key.First]);
            Assert.Equal("20", addresses[Key.Second]);
        }

        [Fact]
        public void Create_NullValue_BecomesEmptyString()
        {
            Addresses addresses = Addresses.Create(new KeyValuePair<Enum, string>(Key.First, null));

            Assert.Equal(string.Empty, addresses[Key.First]);
        }

        [Fact]
        public void Indexer_UnknownKey_Throws()
        {
            Addresses addresses = Addresses.Create(new KeyValuePair<Enum, string>(Key.First, "10"));

            Assert.Throws<KeyNotFoundException>(() => addresses[Key.Second]);
        }

        [Fact]
        public void ContainsKey_ReflectsPresence()
        {
            Addresses addresses = Addresses.Create(new KeyValuePair<Enum, string>(Key.First, "10"));

            Assert.True(addresses.ContainsKey(Key.First));
            Assert.False(addresses.ContainsKey(Key.Second));
        }

        [Fact]
        public void WithOffset_AddsOffsetToEveryValue()
        {
            Addresses addresses = Addresses.Create(
                new KeyValuePair<Enum, string>(Key.First, "10"),
                new KeyValuePair<Enum, string>(Key.Second, "1E"));

            Addresses offset = addresses.WithOffset("2");

            Assert.Equal("12", offset[Key.First]);
            Assert.Equal("20", offset[Key.Second]);
        }

        [Fact]
        public void WithOffset_DoesNotMutateOriginal()
        {
            Addresses addresses = Addresses.Create(new KeyValuePair<Enum, string>(Key.First, "10"));

            addresses.WithOffset("2");

            Assert.Equal("10", addresses[Key.First]);
        }

        [Fact]
        public void WithOffset_PreservesAllKeys()
        {
            Addresses addresses = Addresses.Create(
                new KeyValuePair<Enum, string>(Key.First, "10"),
                new KeyValuePair<Enum, string>(Key.Second, "20"),
                new KeyValuePair<Enum, string>(Key.Third, "30"));

            Addresses offset = addresses.WithOffset("0");

            Assert.True(offset.ContainsKey(Key.First));
            Assert.True(offset.ContainsKey(Key.Second));
            Assert.True(offset.ContainsKey(Key.Third));
        }
    }
}
