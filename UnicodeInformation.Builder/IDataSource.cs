﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System.Unicode.Builder
{
	public interface IDataSource : IDisposable
	{
		Task<Stream> OpenDataFileAsync(string fileName);
	}
}
