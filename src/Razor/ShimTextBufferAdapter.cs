using System;
using System.Web.Razor.Text;

namespace AspNet.Razor_vHalfNext
{
	internal class ShimTextBufferAdapter : ITextBuffer, IShimTextBuffer
    {
		private int _position;

		private string _cachedText;

		private int _cachedPos;

		public Microsoft.VisualStudio.Text.ITextSnapshot Snapshot
		{
			get;
			private set;
		}

		int ITextBuffer.Length
		{
			get
			{
				return Length;
			}
		}

		int ITextBuffer.Position
		{
			get
			{
				return _position;
			}
			set
			{
				_position = value;
			}
		}

        Microsoft.VisualStudio.Text.ITextSnapshot IShimTextBuffer.Snapshot
		{
			get
			{
				return Snapshot;
			}
		}

		private int Length
		{
			get
			{
				return Snapshot.Length;
			}
		}

		public ShimTextBufferAdapter(Microsoft.VisualStudio.Text.ITextSnapshot snapshot)
		{
			Snapshot = snapshot;
			_cachedPos = -1;
		}

		int ITextBuffer.Read()
		{
			return Read();
		}

		int ITextBuffer.Peek()
		{
			return Peek();
		}

		private int Read()
		{
			if (_position >= Snapshot.Length)
			{
				return -1;
			}
			int arg_29_0 = ReadChar();
			_position++;
			return arg_29_0;
		}

		private int Peek()
		{
			if (_position >= Snapshot.Length)
			{
				return -1;
			}
			return ReadChar();
		}

		private int ReadChar()
		{
			if (_cachedPos < 0 || _position < _cachedPos || _position >= _cachedPos + _cachedText.Length)
			{
				_cachedPos = _position;
				int length = Math.Min(1024, Snapshot.Length - _cachedPos);
				_cachedText = Snapshot.GetText(_cachedPos, length);
			}
			return _cachedText[_position - _cachedPos];
		}
	}
}
