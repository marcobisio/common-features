﻿using Serenity.Reporting;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Serenity.Extensions.Repositories
{
    public class ExcelExporter : IExcelExporter
    {
        private readonly IDataReportExcelRenderer renderer;
        private readonly IServiceProvider serviceProvider;

        public ExcelExporter(IDataReportExcelRenderer renderer, IServiceProvider serviceProvider)
        {
            this.renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
            this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public byte[] Export(IEnumerable data, IEnumerable<ReportColumn> columns)
        {
            var report = new TabularDataReport(data, columns);
            return renderer.Render(report);
        }

        public byte[] Export(IEnumerable data, Type columnsType)
        {
            var report = new TabularDataReport(data, columnsType, serviceProvider);
            return renderer.Render(report);
        }

        public byte[] Export(IEnumerable data, Type columnsType, IEnumerable<string> exportColumns)
        {
            var report = new TabularDataReport(data, columnsType, exportColumns, serviceProvider);
            return renderer.Render(report);
        }
    }
}