// using System.Collections.Generic;
//
// namespace FishNet.Utility
// {
//     public class SubsetIterator<T>
//     {
//         /// <summary>
//         /// Current iterated index.
//         /// </summary>
//         private int _currentIndex = 0;
//         
//         /// <summary>
//         /// Iterates through a specified number of items from the list, 
//         /// starting where the last call left off.
//         /// </summary>
//         /// <param name="source">The collection to iterate.</param>
//         /// <param name="iterations">How many items to process in this call.</param>
//         /// <returns>An enumerator yielding the subset of items.</returns>
//         public IEnumerator<T> GetNextSet(List<T> source, int iterations)
//         {
//             if (source == null || iterations == 0)
//                 yield break;
//
//             int listCount = source.Count;
//             if (listCount == 0)
//                 yield break;
//
//             /* If positive then the iterations from the currentIndex
//              * would exceed the list count. When true remove the over
//              * count from iterations, and iterate from the beginning using
//              * the over count value.
//              *
//              * Doing this removes the need to check for out of bounds per
//              * iteration, which scales very well with more iterations. */
//             int overCountIterations = _currentIndex + iterations - listCount;
//             if (overCountIterations > 0)
//                 iterations -= overCountIterations;
//             
//             for (int i = 0; i < iterations; i++)
//             {
//                 T item = source[_currentIndex];
//                 _currentIndex++;
//
//                 yield return item;
//             }
//
//             /* If iterations prior had exceeded the source
//              * count then reset the currentIndex and iterate the
//              * remainder from the start of the source. */
//             if (overCountIterations > 0)
//             {
//                 iterations = overCountIterations;
//                 _currentIndex = 0;
//
//                 for (int i = 0; i < iterations; i++)
//                 {
//                     T item = source[_currentIndex];
//                     _currentIndex++;
//
//                     yield return item;
//                 }
//             }
//         }
//
//         /// <summary>
//         /// Manually resets the iterator to the beginning of the list.
//         /// </summary>
//         public void Reset()
//         {
//             _currentIndex = 0;
//         }
//     }
// }