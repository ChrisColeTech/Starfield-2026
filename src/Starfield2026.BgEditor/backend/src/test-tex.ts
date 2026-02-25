import sharp from 'sharp'

async function main() {
    await sharp('D:/Projects/Starfield-2026/src/Starfield2026.BgEditor/backend/outputs/tr0007_00/front.png')
        .extract({ left: 200, top: 105, width: 110, height: 75 })
        .resize(330, 225, { kernel: 'nearest' })
        .toFile('eyes_crop2.png')
    console.log('Saved eyes_crop2.png')
}
main().catch(console.error)
