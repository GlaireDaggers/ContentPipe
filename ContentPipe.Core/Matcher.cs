using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace ContentPipe.Core
{
    /// <summary>
    /// Class which can be used to match files in a directory
    /// </summary>
    public class Matcher
    {
        private readonly string _includePattern;
        private readonly string _excludePattern;
        private readonly SearchOption _searchOption;

        public Matcher(string includePattern, string excludePattern = null, SearchOption searchOption = SearchOption.AllDirectories)
        {
            _includePattern = includePattern;
            _excludePattern = excludePattern;
            _searchOption = searchOption;
        }

        /// <summary>
        /// Match files in the directory
        /// </summary>
        /// <param name="directory">The directory to search</param>
        /// <returns>An array of all matching files</returns>
        public string[] Match(string directory)
        {
            var included = Directory.GetFiles(directory, _includePattern, _searchOption);

            if (_excludePattern == null) return included;

            var excluded = Directory.GetFiles(directory, _excludePattern, _searchOption);
            return included.Except(excluded).ToArray();
        }
    }
}
