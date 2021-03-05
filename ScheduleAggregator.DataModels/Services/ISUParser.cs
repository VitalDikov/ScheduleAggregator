﻿using ScheduleAggregator.DataModels.Repositories;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Kysect.ItmoScheduleSdk;
using Kysect.ItmoScheduleSdk.Models;
using Kysect.ItmoScheduleSdk.Types;
using ScheduleAggregator.Core.ScheduleItemProviders;
using ScheduleAggregator.DataModels.Enums;
using ScheduleAggregator.DataModels.Entities;
using System.Text.RegularExpressions;

namespace ScheduleAggregator.DataModels.Services
{
    public class ISUParser
    {
        private LessonService lessonService;
        private RoomService roomService;
        private ScheduleService scheduleServide;
        private SemesterService semesterService;
        private SemesterSubjectService semesterSubjectService;
        private StudyCourseService studyCourseService;
        private StudyGroupService studyGroupService;
        private SubjectService subjectService;
        private TeacherService teacherService;

        private ItmoApiProvider _provider = new ItmoApiProvider();
        private List<ScheduleItemModel> _scheduleItems = new List<ScheduleItemModel>();

        public ISUParser(UnitOfWork uof)
        {
            lessonService = new LessonService(uof);
            roomService = new RoomService(uof);
            scheduleServide = new ScheduleService(uof);
            semesterService = new SemesterService(uof);
            semesterSubjectService = new SemesterSubjectService(uof);
            studyCourseService = new StudyCourseService(uof);
            studyGroupService = new StudyGroupService(uof);
            subjectService = new SubjectService(uof);
            teacherService = new TeacherService(uof);


            Init();

            _provider = new ItmoApiProvider();
            _scheduleItems = _provider.ScheduleApi.GetGroupPackSchedule(studyGroupService.Get().Select(_ => _.Name).ToList());
        }
        private void Init()
        {
            Guid course = studyCourseService.Create("Fake_Course");
            semesterService.Create("Fake_Semester", course);

            studyGroupService.Create("M3201", course);
            studyGroupService.Create("M3202", course);
            studyGroupService.Create("M3203", course);
            studyGroupService.Create("M3204", course);
            studyGroupService.Create("M3205", course);
            studyGroupService.Create("M3206", course);
            studyGroupService.Create("M3207", course);
            studyGroupService.Create("M3208", course);
            studyGroupService.Create("M3209", course);
            studyGroupService.Create("M3210", course);
            studyGroupService.Create("M3211", course);
            studyGroupService.Create("M3212", course);
        }

        public void ParseFromISU()
        {
            ParseRooms();
            ParseTeachers();
            ParseSubjects();
            CreateFakeSemesterSubjects();
            ParseLessons();
        }


        private void ParseRooms()
        {
            foreach (var item in _scheduleItems)
            {
                string name = item.Room;
                var campus = GetCampus(item.Place);

                if (roomService.Get(_ => _.Name == name && _.Campus == campus).Any())
                    return;

                roomService.Create(name, campus);
            }
        }

        private void ParseTeachers()
        {
            foreach (var item in _scheduleItems)
            {
                string name = item.Teacher;

                if (teacherService.Get(_ => _.Name == name).Any())
                    return;

                teacherService.Create(name);
            }
        }

        private void ParseSubjects()
        {
            foreach (var item in _scheduleItems)
            {
                string name = item.SubjectTitle.Split('(')[0];

                if (subjectService.Get(_ => _.Name == name).Any())
                    return;
                subjectService.Create(name);
            }
        }

        private void CreateFakeSemesterSubjects()
        {
            Guid fakeSemesterID = semesterService.Get(_ => _.Name == "Fale_Semester").First().Id;
            foreach(var subjectID in subjectService.Get().Select(_ => _.Id))
            {
                semesterSubjectService.Create(subjectID, fakeSemesterID, 0, 0, 0);
            }
        }

        private void ParseLessons()
        {
            foreach (var item in _scheduleItems)
            {
                string name = item.SubjectTitle.Split('(')[0];
                Guid subjectID = semesterSubjectService.Get(_ => _.Subject.Name == name).First().Id;
                var lessonType = ConvertToLessonType(Regex.Match($"{item.SubjectTitle}", @"\(([^)]*)\)").Groups[1].Value);
                Guid teacherID = teacherService.Get(_ => _.Name == item.Teacher).First().Id;
                Campus campus = GetCampus(item.Place);
                Guid roomID = roomService.Get(_ => _.Name == item.Room && _.Campus == campus).First().Id;
                var timeSlot = GetTimeSlot(item.SubjectTitle);
                var daySpot = ConvertToDaySlot(item.DataDay);
                WeekType weekType = ConvertToWeekType(item.DataWeek);
                Guid groupID = studyGroupService.Get(_ => _.Name == item.Group).First().Id;

                lessonService.Create(subjectID, lessonType, groupID, teacherID, roomID, timeSlot, daySpot, weekType);
            }
        }

        private LessonType ConvertToLessonType(string lesson)
        {
            return lesson switch
            {
                "ЛЕК" => LessonType.Lecture,
                "ЛАБ" => LessonType.Laboratory,
                "ПРАК" => LessonType.Practise
            };
        }

        private Campus GetCampus(string campusName)
        {
            if (campusName.Contains("Биржевая линия"))
                return Campus.Birjevaya;
            if (campusName.Contains("Ломоносова"))
                return Campus.Lomonosova;
            if (campusName.Contains("Кронверкский"))
                return Campus.Kronverskiy;

            else return Campus.Undefined;
        }

        private TimeSlot GetTimeSlot(string startTime)
        {
            return startTime switch
            {
                "8:20" => TimeSlot.Lesson1,
                "10:00" => TimeSlot.Lesson2,
                "11:40" => TimeSlot.Lesson3,
                "13:30" => TimeSlot.Lesson4,
                "15:20" => TimeSlot.Lesson5,
                "17:00" => TimeSlot.Lesson6,
                "18:40" => TimeSlot.Lesson7,
                "20:20" => TimeSlot.Lesson8, 
                _ => throw new ArgumentOutOfRangeException("Invalid Time")
            };
        }

        private DaySlot ConvertToDaySlot(DataDayType? day)
        {
            return day switch
            {
                DataDayType.Monday => DaySlot.Monday,
                DataDayType.Tuesday => DaySlot.Tuesday,
                DataDayType.Wednesday => DaySlot.Wednesday,
                DataDayType.Thursday => DaySlot.Thursday,
                DataDayType.Friday => DaySlot.Friday,
                DataDayType.Saturday => DaySlot.Saturday,
            };
        }

        private WeekType ConvertToWeekType(DataWeekType? week)
        {
            return week switch
            {
                DataWeekType.Both => WeekType.Both,
                DataWeekType.Odd => WeekType.Odd,
                DataWeekType.Even => WeekType.Even
            };
        }
    }
}