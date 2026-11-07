import asyncio

import pytest
from bumble import hci
from bumble.core import UUID
from bumble.controller import Controller
from bumble.device import Device
from bumble.gatt import Characteristic, Service
from bumble.link import LocalLink

from hid_bridge import InputEvent
from hogp_bridge import HogpHost


def boot_service():
    mode = Characteristic(UUID.from_16_bits(0x2A4E),
                          Characteristic.Properties.READ | Characteristic.Properties.WRITE_WITHOUT_RESPONSE,
                          'READABLE,WRITEABLE', b'\x01')
    report = Characteristic(UUID.from_16_bits(0x2A22),
                            Characteristic.Properties.READ | Characteristic.Properties.NOTIFY,
                            'READABLE', bytes(8))
    return Service(UUID.from_16_bits(0x1812), [mode, report]), report


@pytest.mark.asyncio
async def test_encrypted_le_boot_keyboard(tmp_path):
    link = LocalLink()
    ds = []
    for i in range(2):
        c = Controller(str(i), link=link, public_address=f'F1:F2:F3:F4:F5:{i:02X}')
        ds.append(Device.with_hci(str(i), hci.Address(bytes(c.public_address), hci.Address.RANDOM_DEVICE_ADDRESS), c, c))
    host, keyboard = ds
    service, report = boot_service()
    keyboard.add_service(service)
    events = []
    receiver = HogpHost(events.append)
    await asyncio.wait_for(asyncio.gather(host.power_on(), keyboard.power_on()), 5)
    await asyncio.wait_for(keyboard.start_advertising(auto_restart=False), 5)
    connection = await host.connect(keyboard.random_address, timeout=5)
    try:
        await asyncio.wait_for(receiver.connect(connection, 'keyboard'), 8)
        assert connection.is_encrypted
        await keyboard.notify_subscribers(report, bytes([0,0,4,0,0,0,0,0]))
        await asyncio.sleep(.02)
        assert InputEvent('key_down', 4) in events
    finally:
        await asyncio.wait_for(connection.disconnect(), 3)
    assert events[-1] == InputEvent('key_up', 4)
    assert not receiver.subscriptions
