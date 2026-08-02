using System;
using System.ComponentModel;
using A2G_Trainer_XP.Model;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>Tests for CoachCompetencyLevel's Total sum, Get/Set dispatch, and change notification.</summary>
    public class CoachCompetencyLevelTests
    {
        [Fact]
        public void Total_SumsAllSixCompetencies()
        {
            CoachCompetencyLevel level = new CoachCompetencyLevel
            {
                Verhandlungsgeschick = 1,
                Motivationsfaehigkeit = 2,
                Trainingsgestaltung = 3,
                Autoritaet = 4,
                Fremdsprachenkenntnisse = 5,
                Ausstrahlung = 6
            };

            Assert.Equal(21, level.Total);
        }

        [Fact]
        public void Get_ForEveryKnownKey_ReturnsTheMatchingProperty()
        {
            CoachCompetencyLevel level = new CoachCompetencyLevel
            {
                Verhandlungsgeschick = 10,
                Motivationsfaehigkeit = 11,
                Trainingsgestaltung = 12,
                Autoritaet = 13,
                Fremdsprachenkenntnisse = 14,
                Ausstrahlung = 15
            };

            Assert.Equal((ushort)10, level.Get(CoachEnums.CompetencyKey.Verhandlungsgeschick));
            Assert.Equal((ushort)11, level.Get(CoachEnums.CompetencyKey.Motivationsfaehigkeit));
            Assert.Equal((ushort)12, level.Get(CoachEnums.CompetencyKey.Trainingsgestaltung));
            Assert.Equal((ushort)13, level.Get(CoachEnums.CompetencyKey.Autoritaet));
            Assert.Equal((ushort)14, level.Get(CoachEnums.CompetencyKey.Fremdsprachenkenntnisse));
            Assert.Equal((ushort)15, level.Get(CoachEnums.CompetencyKey.Ausstrahlung));
        }

        [Fact]
        public void Get_ForUnknownKey_ThrowsArgumentOutOfRangeException()
        {
            CoachCompetencyLevel level = new CoachCompetencyLevel();

            Assert.Throws<ArgumentOutOfRangeException>(() => level.Get((CoachEnums.CompetencyKey)999));
        }

        [Fact]
        public void Set_ForEveryKnownKey_WritesTheMatchingProperty()
        {
            CoachCompetencyLevel level = new CoachCompetencyLevel();

            level.Set(CoachEnums.CompetencyKey.Verhandlungsgeschick, 20);
            level.Set(CoachEnums.CompetencyKey.Motivationsfaehigkeit, 21);
            level.Set(CoachEnums.CompetencyKey.Trainingsgestaltung, 22);
            level.Set(CoachEnums.CompetencyKey.Autoritaet, 23);
            level.Set(CoachEnums.CompetencyKey.Fremdsprachenkenntnisse, 24);
            level.Set(CoachEnums.CompetencyKey.Ausstrahlung, 25);

            Assert.Equal((ushort)20, level.Verhandlungsgeschick);
            Assert.Equal((ushort)21, level.Motivationsfaehigkeit);
            Assert.Equal((ushort)22, level.Trainingsgestaltung);
            Assert.Equal((ushort)23, level.Autoritaet);
            Assert.Equal((ushort)24, level.Fremdsprachenkenntnisse);
            Assert.Equal((ushort)25, level.Ausstrahlung);
        }

        [Fact]
        public void Set_ForUnknownKey_ThrowsArgumentOutOfRangeException()
        {
            CoachCompetencyLevel level = new CoachCompetencyLevel();

            Assert.Throws<ArgumentOutOfRangeException>(() => level.Set((CoachEnums.CompetencyKey)999, 1));
        }

        [Fact]
        public void Verhandlungsgeschick_Set_WithNoSubscriber_DoesNotThrow()
        {
            CoachCompetencyLevel level = new CoachCompetencyLevel();

            Exception thrown = Record.Exception(() => level.Verhandlungsgeschick = 5);

            Assert.Null(thrown);
        }

        [Fact]
        public void Verhandlungsgeschick_Set_WithSubscriber_RaisesPropertyAndTotalChanged()
        {
            CoachCompetencyLevel level = new CoachCompetencyLevel();
            var raised = new System.Collections.Generic.List<string>();
            ((INotifyPropertyChanged)level).PropertyChanged += (s, e) => raised.Add(e.PropertyName);

            level.Verhandlungsgeschick = 5;

            Assert.Contains(nameof(CoachCompetencyLevel.Verhandlungsgeschick), raised);
            Assert.Contains(nameof(CoachCompetencyLevel.Total), raised);
        }
    }
}
