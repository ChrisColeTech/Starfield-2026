namespace MiniToolbox.Core.Bntx;

public interface IBinaryConverter
{
	object Read(BinaryDataReader reader, object instance, BinaryMemberAttribute memberAttribute);

	void Write(BinaryDataWriter writer, object instance, BinaryMemberAttribute memberAttribute, object value);
}
