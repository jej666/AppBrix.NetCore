﻿// Copyright (c) MarinAtanasov. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the project root for license information.
//
using NCrontab;
using System;
using System.Linq;

namespace AppBrix.Events.Schedule.Cron
{
    internal class CronScheduledEvent<T> : IScheduledEvent<T> where T : IEvent
    {
        #region Construction
        public CronScheduledEvent(T args, CrontabSchedule schedule)
        {
            this.Event = args;
            this.schedule = schedule;
        }
        #endregion

        #region Properties
        public T Event { get; }
        #endregion

        #region Public and overriden methods
        public DateTime GetNextOccurrence(DateTime now)
        {
            return this.schedule.GetNextOccurrence(now);
        }
        #endregion

        #region Private fields and constants
        private readonly CrontabSchedule schedule;
        #endregion
    }
}
