import QtQuick 2.9
import QtQuick.Layouts 1.3
import QtQuick.Controls 2.3
import QtQuick.Controls.Material 2.1

ApplicationWindow {
    id: window
    width: 360
    height: 520
    visible: true
    title: "DAB+ radio"

    Material.theme: Material.Light
    Material.accent: '#41cd52'
    Material.primary: '#41cd52'

    Text {
        y:10
        width : 100
        height : 100
        color: "black"
        text: "7C"
        anchors.horizontalCenter: parent.horizontalCenter
    }
}
