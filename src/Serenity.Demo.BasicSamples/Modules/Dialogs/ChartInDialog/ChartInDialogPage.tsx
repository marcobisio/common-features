/** @jsxImportSource jsx-dom */
import { BasicSamplesService } from "@/ServerTypes/Demo";
import { Decorators, TemplatedDialog } from "@serenity-is/corelib";
import { BarController, BarElement, CategoryScale, Chart, Legend, LinearScale } from "chart.js";

Chart.register(BarController, BarElement, CategoryScale, Legend, LinearScale);

const chartColors = ['#4E79A7', '#A0CBE8', '#F28E2B', '#FFBE7D', '#59A14F', '#8CD17D', '#B6992D', '#F1CE63', '#499894', '#86BCB6',
    '#E15759', '#FF9D9A', '#79706E', '#BAB0AC', '#D37295', '#FABFD2', '#B07AA1', '#D4A6C8', '#9D7660', '#D7B5A6'];

export default function pageInit() {
    $('#LaunchDialogButton').click(function (e) {
        (new ChartInDialog()).dialogOpen();
    });
}

@Decorators.registerClass('Serenity.Demo.BasicSamples.ChartInDialog')
@Decorators.resizable()
@Decorators.maximizable()
export class ChartInDialog extends TemplatedDialog<any> {

    private canvasElement: HTMLCanvasElement;

    protected onDialogOpen() {
        super.onDialogOpen();

        BasicSamplesService.OrdersByShipper({}, response => {
            new Chart(this.canvasElement, {
                type: "bar",
                data: {
                    labels: response.Values.map(x => x.Month),
                    datasets: response.ShipperKeys.map((shipperKey, shipperIdx) => ({
                        label: response.ShipperLabels[shipperIdx],
                        backgroundColor: chartColors[shipperIdx % chartColors.length],
                        data: response.Values.map((x, ix) => response.Values[ix][shipperKey])
                    }))
                }
            });
        });
    }

    protected renderContents() {
        this.element.append(<canvas id={`${this.idPrefix}Chart`} ref={(el: HTMLCanvasElement) => this.canvasElement = el}></canvas>);
    }

    protected getDialogOptions() {
        var opt = super.getDialogOptions();
        opt.title = 'Orders by Shipper';
        return opt;
    }
}
