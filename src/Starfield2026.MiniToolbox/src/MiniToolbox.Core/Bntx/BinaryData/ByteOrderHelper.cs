using System;

namespace MiniToolbox.Core.Bntx;

public static class ByteOrderHelper
{
	private static ByteOrder _systemByteOrder;

	public static ByteOrder SystemByteOrder
	{
		get
		{
			if (_systemByteOrder == (ByteOrder)0)
			{
				_systemByteOrder = (BitConverter.IsLittleEndian ? ByteOrder.LittleEndian : ByteOrder.BigEndian);
			}
			return _systemByteOrder;
		}
	}
}
