import { createPlugin } from '/lib/lumeo-vendor/fullcalendar-core@6.1.21/index.js';
import { DayTimeColsView } from './internal.js';
import '/lib/lumeo-vendor/fullcalendar-core@6.1.21/internal.js';
import '/lib/lumeo-vendor/fullcalendar-core@6.1.21/preact.js';
import '/lib/lumeo-vendor/fullcalendar-daygrid@6.1.21/internal.js';

const OPTION_REFINERS = {
    allDaySlot: Boolean,
};

var index = createPlugin({
    name: '@fullcalendar/timegrid',
    initialView: 'timeGridWeek',
    optionRefiners: OPTION_REFINERS,
    views: {
        timeGrid: {
            component: DayTimeColsView,
            usesMinMaxTime: true,
            allDaySlot: true,
            slotDuration: '00:30:00',
            slotEventOverlap: true, // a bad name. confused with overlap/constraint system
        },
        timeGridDay: {
            type: 'timeGrid',
            duration: { days: 1 },
        },
        timeGridWeek: {
            type: 'timeGrid',
            duration: { weeks: 1 },
        },
    },
});

export { index as default };
