import { createPlugin } from '/lib/lumeo-vendor/fullcalendar-core@6.1.21/index.js';
import { DayGridView as DayTableView, TableDateProfileGenerator } from './internal.js';
import '/lib/lumeo-vendor/fullcalendar-core@6.1.21/internal.js';
import '/lib/lumeo-vendor/fullcalendar-core@6.1.21/preact.js';

var index = createPlugin({
    name: '@fullcalendar/daygrid',
    initialView: 'dayGridMonth',
    views: {
        dayGrid: {
            component: DayTableView,
            dateProfileGeneratorClass: TableDateProfileGenerator,
        },
        dayGridDay: {
            type: 'dayGrid',
            duration: { days: 1 },
        },
        dayGridWeek: {
            type: 'dayGrid',
            duration: { weeks: 1 },
        },
        dayGridMonth: {
            type: 'dayGrid',
            duration: { months: 1 },
            fixedWeekCount: true,
        },
        dayGridYear: {
            type: 'dayGrid',
            duration: { years: 1 },
        },
    },
});

export { index as default };
